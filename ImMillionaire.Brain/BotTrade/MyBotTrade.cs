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
    public class MyBotTrade : BaseBot
    {
        public MyBotTrade(IBinanceClientFactory factory, ILogger logger) : base(factory, logger)
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
            else if (rsi14 < 33m && rsi14prev < 30 && rsi14 > rsi14prev && rsi14prev > rsi14prev2)
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
                        if (tokenSource.IsCancellationRequested) break;
                        tokenSource.Cancel();
                        Logger.Information("Cancel CheckBuyWasExecuted");
                        break;
                    case OrderStatus.Filled:
                        PlacedOrder = order;
                        SellLimit();
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
