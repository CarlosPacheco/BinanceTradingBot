using Binance.Net.Enums;
using ImMillionaire.Brain.Core;
using Serilog;
using System.Collections.Generic;
using System.Linq;
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
            decimal rsi14 = candlesticks.Rsi(14).Last().Tick.Value;
            decimal ema10 = candlesticks.Ema(10).Last().Tick.Value;
            decimal ema120 = candlesticks.Ema(120).Last().Tick.Value;
            (decimal? LowerBand, decimal? MiddleBand, decimal? UpperBand) bb20 = candlesticks.Bb(20, 2).Last().Tick;

            if (rsi14 > 70m)
            {
                //  Utils.ErrorLog("overbuyed sell mf");
            }
            else if (rsi14 < 30m)
            {
                BuyLimit();
            }
            //up trend
            if (ema120 < ema10)
            {
                //if (MyCandle.OneHourRsi14 > 70m)
                //{
                //    Utils.SuccessLog("up trend sell");
                //}
            }
            else//down trend
            {
                // Utils.ErrorLog("down trend");
            }

            if (MarketPrice <= bb20.LowerBand)
            {
                // Utils.WarnLog("LowerBand");
            }
            else if (MarketPrice >= bb20.UpperBand)
            {
                //  Utils.WarnLog("UpperBand");
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
                    case OrderStatus.New:
                        PlacedOrder = order;
                        CheckBuyWasExecuted(40);
                        Logger.Information("CheckBuyWasExecuted");
                        break;
                    case OrderStatus.PartiallyFilled:
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
                if (order.Status == OrderStatus.New)
                {
                    Logger.Information("New");
                    PlacedOrder = order;
                }
                else if (order.Status == OrderStatus.Filled)
                {
                    Logger.Information("Sell");
                    PlacedOrder = null;
                }
            }
        }

    }
}
