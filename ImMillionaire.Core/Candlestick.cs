using Binance.Net.Interfaces;
using Trady.Core;

namespace ImMillionaire.Core
{
    public class Candlestick : Candle
    {
        public Candlestick(IBinanceKline binanceKline) : base(binanceKline.OpenTime, binanceKline.Open, binanceKline.High, binanceKline.Low, binanceKline.Close, binanceKline.BaseVolume)
        {
        }
    }
}
