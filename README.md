# UfcFighterStats-01.Backend

**UfcFighterStats** is a web scraper that collects and updates daily statistics of the top 15 UFC Fighters in each weight class.

---

## 🚀 Technologies

![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Quartz Job](https://img.shields.io/badge/Quartz_Job-DC322F?style=for-the-badge&logo=apache-maven&logoColor=white)
![YouTube API](https://img.shields.io/badge/YouTube_API-FF0000?style=for-the-badge&logo=youtube&logoColor=white)
![Google API](https://img.shields.io/badge/Google_API-4285F4?style=for-the-badge&logo=google&logoColor=white)
![Playwright](https://img.shields.io/badge/Playwright-2EAD33?style=for-the-badge&logo=playwright&logoColor=white)

---

## 🌐 Links

- **API Live**: [https://ufcstatsapi.pl/api](https://ufcstatsapi.pl/api)  
- **Frontend Repository**: [GitHub](https://github.com/jedrzejkoriat/UfcFighterStats-02.Frontend)  
- **Frontend Live**: [https://ufcstatsapi.pl](https://ufcstatsapi.pl)

---

## ℹ️ Explanation

The scraper collects data on the **top 15 UFC fighters from each weight class** (128 fighters, resulting in approximately 30,000 lines of data). 

Scraper is scheduled with Quartz Job and retrieves data every day.

It retrieves:  
- **Basic information**: name, nickname, country  
- **Ranking details**: weight class, current ranking, Sherdog profile link  
- **Fight data**: statistics and complete fight history with full fight details  

### Data sources  
- **Wikipedia UFC Rankings** – rankings, weight classes, Sherdog links  
- **Sherdog** – fighter profiles, statistics, and fight histories  
- **Google API** – used when fighters lack a Wikipedia page or Sherdog link  
- **YouTube API** – fetches official *free UFC fights* shared by the UFC

---

## 📦 **Preview**

<img width="1896" height="881" alt="image" src="https://github.com/user-attachments/assets/a69a7cf1-ebcd-4b3d-8515-0412ae1b720c" />

```json5
           {
            "result": "win",
            "opponent": "Adam Dyczka",
            "eventName": "TKO 44 - Hunter vs. Barriault",
            "date": "21-09-2018",
            "method": "TKO (Punches)",
            "round": 2,
            "time": "4:57"
          },
          {
            "result": "win",
            "opponent": "Bobby Sullivan",
            "eventName": "TKO MMA - TKO Fight Night 1",
            "date": "02-08-2018",
            "method": "Submission (Guillotine Choke)",
            "round": 1,
            "time": "1:42"
          }
        ],
        "youtubeVideos": []
      },
      {
        "ranking": 2,
        "name": "Alexander Volkov",
        "nickname": "Drago",
        "country": "Russia",
        "region": "Moscow",
        "age": 36,
        "birthdate": "Oct 24, 1988",
        "weight": 115,
        "height": 200,
        "association": "Strela Team",
        "wins": 38,
        "winKo": 24,
        "winSub": 4,
        "winDec": 10,
        "winOth": 0,
        "losses": 11,
        "lossesKo": 2,
        "lossesSub": 3,
        "lossesDec": 6,
        "lossesOth": 0,
        "noContest": 0,
        "fightHistory": [
          {
            "result": "loss",
            "opponent": "Ciryl Gane",
            "eventName": "UFC 310 - Pantoja vs. Asakura",
            "date": "07-12-2024",
            "method": "Decision (Split)",
            "round": 3,
            "time": "5:00"
          },
          {
            "result": "win",
            "opponent": "Sergei Pavlovich",
            "eventName": "UFC on ABC 6 - Whittaker vs. Aliskerov",
            "date": "22-06-2024",
            "method": "Decision (Unanimous)",
            "round": 3,
```
