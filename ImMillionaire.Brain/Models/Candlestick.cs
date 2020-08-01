using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.Objects.Spot.MarketStream;
using Trady.Core;

namespace ImMillionaire.Brain
{
    public class Candlestick : Candle
    {
        public Candlestick(BinanceKline binanceKline) : base(binanceKline.OpenTime, binanceKline.Open, binanceKline.High, binanceKline.Low, binanceKline.Close, binanceKline.Volume)
        {
        }

        public Candlestick(BinanceStreamKline binanceStreamKline) : base(binanceStreamKline.OpenTime, binanceStreamKline.Open, binanceStreamKline.High, binanceStreamKline.Low, binanceStreamKline.Close, binanceStreamKline.Volume)
        {
        }


    }
}
