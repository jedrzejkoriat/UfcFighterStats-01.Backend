namespace UfcStatsAPI.Helpers;

public static class EdgeCaseHelper
{
    public static string HandleEdgeCases(string sherdogLink)
    {
        // Remove the ' from the link
        if (sherdogLink.Contains("&#39;"))
        {
            sherdogLink = sherdogLink.Replace("&#39;", "");
        }

        return sherdogLink;
    }
}
