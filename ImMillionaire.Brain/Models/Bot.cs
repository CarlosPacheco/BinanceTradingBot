﻿using Binance.Net.Enums;
using ImMillionaire.Brain.Core.Enums;
using System.Text.Json.Serialization;

namespace ImMillionaire.Brain.Models
{
    public class Bot
    {
        public int Id { get; set; }

        public string Symbol { get; set; }

        public WalletType Type { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public KlineInterval KlineInterval { get; set; }

        public bool UseAllAmount { get; set; }

        public decimal InitAmount { get; set; }

        public decimal WonAmount { get; set; }

        public decimal Amount { get; set; }

        public decimal SellMarginOfSafe { get; set; }

        public decimal BuyMarginOfSafe { get; set; }
    }
}
