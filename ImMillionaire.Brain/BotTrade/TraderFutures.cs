using Binance.Net.Enums;
using ImMillionaire.Brain.Core;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using Trady.Analysis.Extension;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain
{
    public class TraderFutures : BaseBot
    {
        MyCandle MyCandle = new MyCandle();

        public TraderFutures(IBinanceClientFactory factory, ILogger logger) : base(factory, logger)
        {
        }

        protected override void RegisterCandlestickUpdates()
        {
            SubscribeCandlesticks(KlineInterval.OneHour, (candlestick, candle) => MyCandle.SetOneHour(candlestick));
        }

        protected override void OrderUpdate(Order order)
        {
            // Handle order update info data
            if (order.Side == OrderSide.Buy && order.Status == OrderStatus.Filled)
            {
                PlacedOrder = order;
                Logger.Information("buy future");
                SellLimit();
            }
            else if (order.Side == OrderSide.Sell && order.Status == OrderStatus.Filled)
            {
                Logger.Information("sell future");
                PlacedOrder = null;
            }
            else if (order.Side == OrderSide.Buy && order.Status == OrderStatus.Canceled)
            {
                Logger.Information("cancel buy future");
                PlacedOrder = null;
            }
        }

        bool? xptoUp = null;
        bool? xptoDown = null;

        DateTime lastDateUp = DateTime.UtcNow;
        DateTime lastDateDown = DateTime.UtcNow;

        protected override void KlineUpdates(IList<IOhlcv> candlestick, Candlestick candle)
        {
            decimal rsi14 = candlestick.Rsi(14).Last().Tick.Value;
            decimal ema10 = candlestick.Ema(10).Last().Tick.Value;
            decimal ema120 = candlestick.Ema(120).Last().Tick.Value;
            (decimal? LowerBand, decimal? MiddleBand, decimal? UpperBand) bb20 = candlestick.Bb(20, 2).Last().Tick;

            if (rsi14 > 70m)
            {
                // SellNow();
                //  Utils.ErrorLog("overbuyed sell mf");
            }
            else if (rsi14 < 30m)
            {

                // Utils.SuccessLog("oversell buy mf");
            }
            //up trend
            if (ema120 < ema10)
            {
                lastDateUp = DateTime.UtcNow;
                if ((DateTime.UtcNow - lastDateDown).TotalSeconds > 10)
                {
                    if (MyCandle.OneHourRsi14 > 70m)
                    {
                        Logger.Information("up trend sell future");
                    }

                    if (xptoUp.HasValue && xptoUp == true)
                    {
                        BuyLimit();
                        Logger.Information("up trend buy future");
                        xptoUp = false;
                        xptoDown = true;
                    }
                    else
                    {
                        xptoDown = true;
                    }
                }
            }
            else//down trend
            {
                lastDateDown = DateTime.UtcNow;
                if ((DateTime.UtcNow - lastDateUp).TotalSeconds > 10)
                {
                    if (xptoDown.HasValue && xptoDown == true)
                    {
                        //  SellNow();
                        Logger.Information("Bot end init");
                        xptoDown = false;
                        xptoUp = true;
                    }
                    else
                    {
                        xptoUp = true;
                    }
                }
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

        public override void BuyLimit()
        {
            if (PlacedOrder == null)
            {
                decimal freeBalance = BinanceClient.GetFreeQuoteBalance();
                if (freeBalance > 1 && OrderBook != null)
                {
                    decimal price = OrderBook.LastBidPrice;

                    decimal amount = freeBalance / price;

                    if (BinanceClient.TryPlaceOrder(OrderSide.Buy, OrderType.Limit, amount, price, TimeInForce.GoodTillCancel, out Order order))
                    {
                        PlacedOrder = order;
                        CheckBuyWasExecuted();
                        Logger.Warning("future place buy at: {0} {1}", price, OrderBook.LastAskPrice);
                    }
                    else
                    {
                        Logger.Warning("future error place buy at: {0} {1}", price, amount);
                    }
                }

            }
        }

        public override void SellLimit()
        {
            decimal percentage = 0.10m;
            decimal amount = PlacedOrder.Quantity;
            decimal newPrice = PlacedOrder.Price + PlacedOrder.Price * (percentage / 100);

            try
            {
                if (BinanceClient.TryPlaceOrder(OrderSide.Sell, OrderType.Limit, amount, newPrice, TimeInForce.GoodTillCancel, out Order order))
                {
                    Logger.Warning("place sell at: {0}", newPrice);
                }
            }
            catch (Exception ex)
            {
                Logger.Fatal("error sell price: {0} amount: {1} {2}", newPrice, amount, ex.Message);
            }
        }

    }

}
