using HtmlAgilityPack;
using Microsoft.Playwright;
using System.Globalization;
using System.Text.RegularExpressions;
using UfcStatsAPI.Contracts;
using UfcStatsAPI.Model;

namespace UfcStatsAPI.Services;

public class SherdogService : ISherdogService
{
    private readonly ILogger<SherdogService> _logger;

    public SherdogService(ILogger<SherdogService> logger)
    {
        _logger = logger;
    }

    // Scraper for fighter statistics and fight history from Sherdog page
    public async Task<FighterModel> ScrapStatsAsync(string url, string ranking, bool firstHalf)
    {
        // Use Playwright to load the page and get the content
        int attempt = 0;
        int maxRetries = 10;
        string link = "https://www.sherdog.com/fighter/" + url;
        string? content = null;

        // Try to load the page multiple times
        while (attempt < maxRetries && content == null)
        {
            attempt++;
            IBrowser? browser = null;

            try
            {
                using var playwright = await Playwright.CreateAsync();
                browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = false
                });

                var page = await browser.NewPageAsync(new BrowserNewPageOptions
                {
                    ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Safari/537.36"
                });
                _logger.LogInformation("A - Browser created: {url}", url);

                await page.GotoAsync(link, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 5000 });
                _logger.LogInformation("B - Page opened: {url}", url);

                await page.WaitForSelectorAsync("div.bio-holder", new PageWaitForSelectorOptions { Timeout = 5000 });
                _logger.LogInformation("C - Content found: {url}", url);

                content = await page.ContentAsync();
                _logger.LogInformation("D - Content acquired: {url}", url);
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "X - Error: {url}", url);
                await Task.Delay(2000);
            }
            finally
            {
                if (browser != null)
                {
                    await browser.CloseAsync();
                    _logger.LogInformation("E - Browser closed: {url}", url);
                }
            }

            _logger.LogInformation("Y - Try-catch block left : {url}", url);
        }

        if (content == null)
        {
            _logger.LogWarning("F - Content empty: {url}", url);
            return null;
        }

        // Load content into HtmlAgilityPack
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(content);
        _logger.LogInformation("F - Content loaded: {url}", url);

        // Scrap fighter data
        FighterModel fighterModel = ScrapFighterData(htmlDoc, ranking);
        _logger.LogInformation("G - Content scrapped: {url}", url);

        return fighterModel;
    }

    private FighterModel ScrapFighterData(HtmlDocument htmlDoc, string ranking)
    {
        var fighter = new FighterModel();

        fighter.Ranking = Convert.ToInt32(ranking);

        // Age, Height
        var bioHtml = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='bio-holder']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

        for (int i = 0; i < bioHtml.Length; i++)
        {
            if (bioHtml[i].Contains("AGE"))
            {
                // Age
                int age = Convert.ToInt32(Regex.Match(bioHtml[i + 1], @"<td><b>(\d+)").ToString().Substring(7));
                fighter.Age = age;
            }
            if (bioHtml[i].Contains("birthDate"))
            {
                // Birthdate
                string birthdate = Regex.Match(bioHtml[i], @">([^<]+)</span>").ToString();
                birthdate = birthdate.Substring(1, birthdate.Length - "</span>".Length - 1);
                fighter.Birthdate = birthdate;
            }
            if (bioHtml[i].Contains("WEIGHT"))
            {
                // Weight
                string weight = Regex.Match(bioHtml[i], @"(\d+)\.").Groups[1].Value;
                fighter.Weight = int.Parse(weight);
            }
            if (bioHtml[i].Contains("HEIGHT"))
            {
                // Height
                int height = Convert.ToInt32(Regex.Match(bioHtml[i], @"(\d+\.\d+)\s*cm").ToString().Substring(0, 3));
                fighter.Height = height;
            }
            if (bioHtml[i].Contains("ASSOCIATION"))
            {
                // Association
                string association = Regex.Match(bioHtml[i + 1], @">([^<]+)<").ToString().Substring(1).TrimEnd('<');
                fighter.Association = association;
            }
        }

        // Name + Surname, Nickname, Country
        var fighterTitleHtml = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='fighter-title']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

        for (int i = 0; i < fighterTitleHtml.Length; i++)
        {
            if (fighterTitleHtml[i].Contains("fighter-nationality"))
            {
                // Country
                string country = Regex.Match(fighterTitleHtml[i + 2], @">(.*?)<").ToString().TrimEnd('<').Substring(1);
                fighter.Country = country;
            }
            if (fighterTitleHtml[i].Contains("addressLocality"))
            {
                // Region
                string region = Regex.Match(fighterTitleHtml[i], @">([^<]+)<").ToString().Substring(1).TrimEnd('<');
                fighter.Region = region;
            }
            if (fighterTitleHtml[i].Contains("fn"))
            {
                // Name and Surname
                string name = Regex.Match(fighterTitleHtml[i], @"fn(.*?)<").ToString().TrimEnd('<').Substring(4);
                fighter.Name = name;
            }
            if (fighterTitleHtml[i].Contains("nickname"))
            {
                // Nickname
                Match match = Regex.Match(fighterTitleHtml[i], @"<em>(.*?)<");
                string nickname = "";

                if (match.Success)
                {
                    nickname = match.ToString().TrimEnd('<').Substring(4);
                }

                fighter.Nickname = nickname;
            }
        }

        // Wins, WinsKo, WinsSub, WinsDec, WinsOthers
        var recordHtml = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='winsloses-holder']");

        var winsHtml = recordHtml.SelectSingleNode("//div[@class='wins']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

        // Initialize fields that sometimes are not present
        int winOthers = 0;

        for (int i = 0; i < winsHtml.Count(); i++)
        {
            if (winsHtml[i].Contains(">Wins<"))
            {
                // Wins
                int wins = Convert.ToInt32(Regex.Match(winsHtml[i + 1], @">(\d+)<").ToString().Trim('<').Substring(1));
                fighter.Wins = wins;
            }
            if (winsHtml[i].Contains("em> TKO"))
            {
                // Wins by KO/TKO
                int winsKo = Convert.ToInt32(Regex.Match(winsHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
                fighter.WinsKo = winsKo;
            }
            if (winsHtml[i].Contains(">SUBMISSIONS"))
            {
                // Wins by Submission
                int winSub = Convert.ToInt32(Regex.Match(winsHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
                fighter.WinsSub = winSub;
            }
            if (winsHtml[i].Contains(">DECISIONS"))
            {
                // Wins by Decision
                int winDec = Convert.ToInt32(Regex.Match(winsHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
                fighter.WinsDec = winDec;
            }
            if (winsHtml[i].Contains(">OTHERS"))
            {
                // Other wins
                winOthers = Convert.ToInt32(Regex.Match(winsHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
            }
        }

        // Add fields that sometimes are not present as zeros
        fighter.WinsOth = winOthers;

        // Losses, LossesKo, LossesSub, LossesDec, LossesOthers, NoContest
        var lossesHtml = recordHtml.SelectSingleNode("//div[@class='loses']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

        // Initialize fields that sometimes are not present
        int lossesOthers = 0;
        int noContest = 0;

        for (int i = 0; i < lossesHtml.Count(); i++)
        {
            if (lossesHtml[i].Contains(">Losses<"))
            {
                // Loses
                int losses = Convert.ToInt32(Regex.Match(lossesHtml[i + 1], @">(\d+)<").ToString().Trim('<').Substring(1));
                fighter.Losses = losses;
            }
            if (lossesHtml[i].Contains("em> TKO"))
            {
                // Lose by KO/TKO
                int lossesKo = Convert.ToInt32(Regex.Match(lossesHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
                fighter.LossesKo = lossesKo;
            }
            if (lossesHtml[i].Contains(">SUBMISSIONS"))
            {
                // Lose by Submission
                int lossesSub = Convert.ToInt32(Regex.Match(lossesHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
                fighter.LossesSub = lossesSub;
            }
            if (lossesHtml[i].Contains(">DECISIONS"))
            {
                // Lose by Decision
                int lossesDec = Convert.ToInt32(Regex.Match(lossesHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
                fighter.LossesDec = lossesDec;
            }
            if (lossesHtml[i].Contains(">OTHERS"))
            {
                // Other loses
                lossesOthers = Convert.ToInt32(Regex.Match(lossesHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
            }
            if (lossesHtml[i].Contains("winloses nc"))
            {
                // No contest
                noContest = Convert.ToInt32(Regex.Match(lossesHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
            }
        }

        // Add fields that sometimes are not present as zeros
        fighter.LossesOth = lossesOthers;
        fighter.NoContest = noContest;

        // Fights: Result, Opponent, Event, Date, Method, Round, Time
        var fightHistoryHtml = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='module fight_history']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

        for (int i = 12; i < fightHistoryHtml.Count(); i++)
        {
            if (fightHistoryHtml[i].Contains("<tr>"))
            {
                FightModel fight = new FightModel();
                // Fight result
                fight.Result = Regex.Match(fightHistoryHtml[i + 1], @">(\w+)<").ToString().Trim('<').Substring(1);

                // Opponent
                fight.Opponent = Regex.Match(fightHistoryHtml[i + 2], @">([^<]+)<").ToString().Trim('<').Substring(1);

                // Event Name
                fight.EventName = Regex.Match(fightHistoryHtml[i + 3], ">([^<]+)<").ToString().Trim('<').Substring(1);

                // Date
                string scrappedDate = Regex.Match(fightHistoryHtml[i + 3], @"[A-Za-z]{3} / \d{2} / \d{4}").ToString();
                DateTime date = DateTime.ParseExact(scrappedDate, "MMM / dd / yyyy", CultureInfo.InvariantCulture);
                fight.Date = date.ToString("dd-MM-yyyy");

                // Method
                fight.Method = Regex.Match(fightHistoryHtml[i + 4], "b>([^<]+)<").ToString().Trim('<').Substring(2);

                // Round of stoppage
                fight.Round = Convert.ToInt32(Regex.Match(fightHistoryHtml[i + 6], @">(\w+)<").ToString().Trim('<').Substring(1));

                // Time of stoppage
                fight.Time = Regex.Match(fightHistoryHtml[i + 7], ">([^<]+)<").ToString().Trim('<').Substring(1);

                fighter.FightHistory.Add(fight);
                i += 8;
            }
        }

        return fighter;
    }
}
