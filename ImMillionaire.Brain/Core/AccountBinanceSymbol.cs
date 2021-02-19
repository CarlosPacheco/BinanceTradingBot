using Binance.Net.Objects.Futures.MarketData;
using Binance.Net.Objects.Spot.MarketData;
using BinanceSymbolLotSizeFilter = Binance.Net.Objects.Spot.MarketData.BinanceSymbolLotSizeFilter;

namespace ImMillionaire.Brain.Core
{
    public class AccountBinanceSymbol
    {
        /// <summary>
        /// The symbol
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The base asset
        /// </summary>
        public string BaseAsset { get; set; }

        /// <summary>
        /// The quote asset
        /// </summary>
        public string QuoteAsset { get; set; }

        /// <summary>
        /// The precision of the base asset
        /// </summary>
        public int BaseAssetPrecision { get; set; }

        /// <summary>
        /// The quantity precision
        /// </summary>
        public int QuantityPrecision { get; set; }

        public BinanceSymbolLotSizeFilter LotSizeFilter { get; set; }

        public Binance.Net.Objects.Spot.MarketData.BinanceSymbolPriceFilter PriceFilter { get; set; }

        public AccountBinanceSymbol(BinanceSymbol binanceSymbol)
        {
            Name = binanceSymbol.Name;
            BaseAsset = binanceSymbol.BaseAsset;
            QuoteAsset = binanceSymbol.QuoteAsset;
            BaseAssetPrecision = binanceSymbol.BaseAssetPrecision;
            QuantityPrecision = binanceSymbol.QuoteAssetPrecision;
            LotSizeFilter = binanceSymbol.LotSizeFilter;
            PriceFilter = binanceSymbol.PriceFilter;
        }

        public AccountBinanceSymbol(BinanceFuturesSymbol binanceSymbol)
        {
            Name = binanceSymbol.Name;
            BaseAsset = binanceSymbol.BaseAsset;
            QuoteAsset = binanceSymbol.QuoteAsset;
            BaseAssetPrecision = binanceSymbol.BaseAssetPrecision;
            QuantityPrecision = binanceSymbol.QuantityPrecision;
            LotSizeFilter = new BinanceSymbolLotSizeFilter()
            {
                MinQuantity = binanceSymbol.LotSizeFilter.MinQuantity,
                MaxQuantity = binanceSymbol.LotSizeFilter.MaxQuantity,
                StepSize = binanceSymbol.LotSizeFilter.StepSize
            };
            PriceFilter = new Binance.Net.Objects.Spot.MarketData.BinanceSymbolPriceFilter()
            {
                FilterType = binanceSymbol.PriceFilter.FilterType,
                MinPrice = binanceSymbol.PriceFilter.MinPrice,
                MaxPrice = binanceSymbol.PriceFilter.MaxPrice,
                TickSize = binanceSymbol.PriceFilter.TickSize
            };
        }
    }
}