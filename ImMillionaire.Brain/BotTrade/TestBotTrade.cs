using Binance.Net.Enums;
using ImMillionaire.Brain.Core;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using Trady.Analysis;
using Trady.Analysis.Extension;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain
{
    public class TestBotTrade : BaseBot
    {
        public TestBotTrade(IBinanceClientFactory factory, ILogger logger) : base(factory, logger)
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
            decimal rsi14prev = rsi[rsi.Count - 1].Tick.Value;
            if (rsi14 > 70m)
            {
                SellLimit();
            }
            else if (rsi14 < 39m && rsi14prev < 30 && rsi14 > rsi14prev)
            {
                BuyLimit();
            }
        }

        protected override void OrderUpdate(Order order)
        {
            Logger.Information("Order {@Order}", order);
            // Handle order update info data
            if (order.Side == OrderSide.Buy)
            {
                switch (order.Status)
                {
                    case OrderStatus.PartiallyFilled:
                        tokenSource.Cancel();
                        Logger.Information("Cancel CheckBuyWasExecuted");
                        break;
                    case OrderStatus.Filled:
                        PlacedOrder = order;
                        break;
                    case OrderStatus.Canceled:
                    case OrderStatus.PendingCancel:
                    case OrderStatus.Rejected:
                    case OrderStatus.Expired:
                        Logger.Information("Cancel buy");
                        PlacedOrder = null;
                        break;
                    default:
                        break;
                }
            }
            else //OrderSide.Sell
            {
                if (order.Status == OrderStatus.Filled)
                {
                    Logger.Information("Sell");
                    PlacedOrder = null;
                }
            }
        }

    }
}
