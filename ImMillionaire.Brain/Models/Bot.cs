using Binance.Net.Enums;
using System.Text.Json.Serialization;

namespace ImMillionaire.Brain.Models
{
    public class Bot
    {
        public int Id { get; set; }

        public string Symbol { get; set; }

        public ImMillionaire.Core.Enums.WalletType Type { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public KlineInterval KlineInterval { get; set; }

        public bool UseAllAmount { get; set; }

        public decimal InitAmount { get; set; }

        public decimal WonAmount { get; set; }

        public decimal Amount { get; set; }

        public decimal SellMarginOfSafe { get; set; }

        public decimal BuyMarginOfSafe { get; set; }

        /// <summary>
        /// Time in seconds to wait before cancel a order not yet execute
        /// </summary>
        public int WaitSecondsBeforeCancelOrder { get; set; }

        /// <summary>
        /// Place a sell order when the buy order was filled
        /// </summary>
        public bool PlaceSellWhenBuyFilled { get; set; }
    }
}
