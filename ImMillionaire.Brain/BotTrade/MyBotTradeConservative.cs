using Binance.Net.Enums;
using ImMillionaire.Brain.Core;
using Serilog;
using System;
using System.Collections.Generic;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain
{
    public class MyBotTradeConservative : BaseBot
    {
        MyCandle MyCandle = new MyCandle();

        public MyBotTradeConservative(IBinanceClientFactory factory, ILogger logger) : base(factory, logger)
        {
        }

        protected override void RegisterCandlestickUpdates()
        {
            SubscribeCandlesticks(KlineInterval.ThreeMinutes, (candlestick, candle) => MyCandle.SetThreeMinutes(candlestick));
            SubscribeCandlesticks(KlineInterval.OneHour, (candlestick, candle) => MyCandle.SetOneHour(candlestick));
        }

        protected override void KlineUpdates(IList<IOhlcv> candlesticks, Candlestick candle)
        {
            if (MyCandle.OneHourRsi14 > 70m)
            {
                if (PlacedOrder != null) SellLimit();
            }
            else if (MyCandle.OneHourRsi14 < 30m)
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
                    case OrderStatus.New:
                        PlacedOrder = order;
                        CheckBuyWasExecuted(60);
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

        public override void SellLimit()
        {
            decimal percentage = 0.15m;
            decimal fee = 0.075m; //BNB fee

            decimal amount = PlacedOrder.Quantity;
            if (PlacedOrder.Commission > 0)
            {
                amount = Utils.TruncateDecimal(PlacedOrder.Quantity - (PlacedOrder.Quantity * (fee / 100)), BinanceClient.DecimalAmount);//BNB fee
                Logger.Warning("buy Commission sell at: {0}", PlacedOrder.Quantity * 0.00075m);
                percentage += fee;//recovery the fee
            }

            decimal newPrice = decimal.Round(PlacedOrder.Price + PlacedOrder.Price * (percentage / 100), 2);
            try
            {
                if (BinanceClient.TryPlaceOrder(OrderSide.Sell, OrderType.Limit, amount, newPrice, TimeInForce.GoodTillCancel, out Order order))
                {
                    PlacedOrder = order;
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
