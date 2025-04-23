namespace UfcStatsAPI.Model
{
    public class SherdogLinksWeightClassModel
    {
        public string? Name { get; set; }
        public List<SherdogLinksFighterModel> Fighters { get; set; } = new List<SherdogLinksFighterModel>();
    }
}
