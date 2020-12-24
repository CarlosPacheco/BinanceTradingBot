using System;
using System.Collections.Generic;
using System.Linq;
using Trady.Analysis.Extension;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain
{
    public class MyCandle
    {
        public decimal OneDayEma10 { get; set; }

        public decimal OneDayEma25 { get; set; }

        public decimal OneDayRsi14 { get; set; }

        public decimal FourHourEma10 { get; set; }

        public decimal FourHourEma60 { get; set; }

        public decimal FourHourRsi14 { get; set; }

        public decimal OneHourEma10 { get; set; }

        public decimal OneHourEma120 { get; set; }

        public decimal OneHourRsi14 { get; set; }
        public decimal ThreeMinutesEma10 { get; private set; }
        public decimal ThreeMinutesEma120 { get; private set; }

        public void SetOneDay(IList<IOhlcv> candlestick)
        {
            OneDayEma10 = candlestick.Ema(10).Last().Tick.Value;
            OneDayRsi14 = candlestick.Rsi(14).Last().Tick.Value;
            OneDayEma25 = candlestick.Ema(25).Last().Tick.Value;
        }

        public void SetThreeMinutes(IList<IOhlcv> candlestick)
        {
            ThreeMinutesEma10 = candlestick.Ema(10).Last().Tick.Value;
            ThreeMinutesEma120 = candlestick.Ema(120).Last().Tick.Value;
        }

        public void SetFourHour(IList<IOhlcv> candlestick)
        {
            FourHourEma10 = candlestick.Ema(10).Last().Tick.Value;
            FourHourRsi14 = candlestick.Rsi(14).Last().Tick.Value;
            FourHourEma60 = candlestick.Ema(60).Last().Tick.Value;
        }

        public void SetOneHour(IList<IOhlcv> candlestick)
        {
            OneHourEma10 = candlestick.Ema(10).Last().Tick.Value;
            OneHourRsi14 = candlestick.Rsi(14).Last().Tick.Value;
            OneHourEma120 = candlestick.Ema(120).Last().Tick.Value;
        }
    }


}
