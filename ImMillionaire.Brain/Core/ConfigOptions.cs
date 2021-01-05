namespace ImMillionaire.Brain.Core
{
    public class ConfigOptions
    {
        public const string Position = "Config";

        public string Symbol { get; set; }

        public string ApiKey { get; set; }

        public string SecretKey { get; set; }

        public decimal SellPercentage { get; set; }

        public decimal BuyMarginOfSafe { get; set; }
    }
}
