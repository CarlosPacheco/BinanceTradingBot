using Binance.Net.Objects.Futures.MarketData;
using Binance.Net.Objects.Spot.MarketData;

namespace ImMillionaire.Brain.Core
{
    public class AccountBinanceSymbol
    {
        /// <summary>
        /// The precision of the base asset
        /// </summary>
        public int BaseAssetPrecision { get; set; }

        /// <summary>
        /// The quantity precision
        /// </summary>
        public int QuantityPrecision { get; set; }

        public AccountBinanceSymbol(BinanceSymbol binanceSymbol)
        {
            BaseAssetPrecision = binanceSymbol.BaseAssetPrecision;
            QuantityPrecision = binanceSymbol.QuoteAssetPrecision;
        }

        public AccountBinanceSymbol(BinanceFuturesSymbol binanceSymbol)
        {
            BaseAssetPrecision = binanceSymbol.BaseAssetPrecision;
            QuantityPrecision = binanceSymbol.QuantityPrecision;
        }
    }
}