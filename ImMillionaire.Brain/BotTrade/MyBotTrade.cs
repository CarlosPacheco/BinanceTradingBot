using ImMillionaire.Brain.Core;
using ImMillionaire.Core;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using Trady.Analysis;
using Trady.Analysis.Extension;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain
{
    public class MyBotTrade : BaseBot
    {
        public MyBotTrade(IBinanceClientFactory factory, ILogger<MyBotTrade> logger) : base(factory, logger)
        {
        }

        protected override void RegisterCandlestickUpdates()
        {
            //SubscribeCandlesticks(KlineInterval.OneHour, (candlestick, candle) => MyCandle.SetOneHour(candlestick));
        }

        protected override void KlineUpdates(IList<IOhlcv> candlesticks, Candlestick candle)
        {
            IReadOnlyList<AnalyzableTick<decimal?>> rsi = candlesticks.Rsi(14);

            if (rsi.Count < 3) return;

            decimal rsi14 = rsi.Last().Tick.Value;
            decimal rsi14prev = rsi[rsi.Count - 2].Tick.Value;
            decimal rsi14prev2 = rsi[rsi.Count - 3].Tick.Value;

            if (rsi14 > 70m)
            {
                //  Utils.ErrorLog("overbuyed sell mf");
            }
            else if (rsi14prev < 36m && rsi14prev2 < 30 && (rsi14 - 3m) > rsi14prev && (rsi14prev - 2m) > rsi14prev2)
            {
                BuyLimit();
            }
        }

    }
}
