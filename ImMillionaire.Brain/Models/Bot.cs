using ImMillionaire.Brain.Core.Enums;

namespace ImMillionaire.Brain.Models
{
    public class Bot
    {
        public int Id { get; set; }

        public string Symbol { get; set; }

        public WalletType Type { get; set; }

        public bool UseAllAmount { get; set; }

        public decimal InitAmount { get; set; }

        public decimal WonAmount { get; set; }

        public decimal Amount { get; set; }

        public decimal SellMarginOfSafe { get; set; }

        public decimal BuyMarginOfSafe { get; set; }
    }
}
