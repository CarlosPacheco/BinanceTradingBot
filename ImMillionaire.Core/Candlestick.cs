using Binance.Net.Interfaces;
using Trady.Core;

namespace ImMillionaire.Core
{
    public class Candlestick : Candle
    {
        public Candlestick(IBinanceKline binanceKline) : base(binanceKline.OpenTime, binanceKline.OpenPrice, binanceKline.HighPrice, binanceKline.LowPrice, binanceKline.ClosePrice, binanceKline.Volume)
        {
        }
    }
}
