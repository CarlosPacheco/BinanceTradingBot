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
    public class TestBotTrade : BaseBot
    {
        public TestBotTrade(IBinanceClientFactory factory, ILogger<TestBotTrade> logger) : base(factory, logger)
        {
        }

        protected override void RegisterCandlestickUpdates()
        {
        }

        protected override void KlineUpdates(IList<IOhlcv> candlesticks, Candlestick candle)
        {
            IReadOnlyList<AnalyzableTick<decimal?>> rsi = candlesticks.Rsi(14);

            if (rsi.Count < 2) return;

            decimal rsi14 = rsi.Last().Tick.Value;
            decimal rsi14prev = rsi[rsi.Count - 2].Tick.Value;
            if (rsi14 > 67m)
            {
                SellLimit();
            }
            else if (rsi14 < 39m && rsi14prev < 30 && rsi14 > rsi14prev)
            {
                BuyLimit();
            }
        }
    }
}
