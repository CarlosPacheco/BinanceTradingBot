using Binance.Net.Enums;
using ImMillionaire.Brain.Core;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using Trady.Analysis.Extension;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain
{
    public class MyBotTrade : BaseBot
    {
        decimal marketPrice;

        public ConfigOptions Config { get; }

        public MyBotTrade(IBinanceClientFactory factory, IOptions<ConfigOptions> config) : base(factory, Core.Enums.WalletType.Margin)
        {
            Config = config.Value;
        }

        protected override void RegisterCandlestickUpdates()
        {
            SubscribeCandlesticks(KlineInterval.OneMinute, KlineUpdates);
            //SubscribeCandlesticks(KlineInterval.OneHour, (candlestick, candle) => MyCandle.SetOneHour(candlestick));
        }

        protected void KlineUpdates(IList<IOhlcv> candlestick, Candlestick candle)
        {
            marketPrice = candle.Close;
            // Log.Information($"market price: {marketPrice}", ConsoleColor.DarkYellow);

            decimal rsi14 = candlestick.Rsi(14).Last().Tick.Value;
            decimal ema10 = candlestick.Ema(10).Last().Tick.Value;
            decimal ema120 = candlestick.Ema(120).Last().Tick.Value;
            (decimal? LowerBand, decimal? MiddleBand, decimal? UpperBand) bb20 = candlestick.Bb(20, 2).Last().Tick;

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

            if (marketPrice <= bb20.LowerBand)
            {
                // Utils.WarnLog("LowerBand");
            }
            else if (marketPrice >= bb20.UpperBand)
            {
                //  Utils.WarnLog("UpperBand");
            }
        }

        protected override void OrderUpdate(Order order)
        {
            Log.Information("Order {@Order}", order);
            // Handle order update info data
            if (order.Side == OrderSide.Buy)
            {
                switch (order.Status)
                {
                    case OrderStatus.New:
                        PlacedOrder = order;
                        CheckBuyWasExecuted(40);
                        break;
                    case OrderStatus.PartiallyFilled:
                        tokenSource.Cancel();
                        Log.Information("Cancel CheckBuyWasExecuted");
                        break;
                    case OrderStatus.Filled:
                        PlacedOrder = order;
                        SellLimit();
                        break;
                    case OrderStatus.Canceled:
                    case OrderStatus.PendingCancel:
                    case OrderStatus.Rejected:
                    case OrderStatus.Expired:
                        Log.Information("Cancel buy");
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
                    Log.Information("New");
                    PlacedOrder = null;
                }
                else if (order.Status == OrderStatus.Filled)
                {
                    Log.Information("Sell");
                    PlacedOrder = null;
                }
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

                    if (marketPrice > 0)
                    {
                        // margin of safe to buy in the best price 0.02%
                        decimal marginOfSafe = Config.BuyMarginOfSafe;
                        while (price >= marketPrice)
                        {
                            decimal bidAskSpread = decimal.Round((OrderBook.LastAskPrice - OrderBook.LastBidPrice) / OrderBook.LastAskPrice * 100, 3);
                            decimal dynamicMarginOfSafe = decimal.Round(price * 100 / marketPrice - 100, 3);
                            Log.Warning("place buy Market: {0} Bid: {1}", marketPrice, price);
                            if (dynamicMarginOfSafe > marginOfSafe) marginOfSafe = dynamicMarginOfSafe;
                            Log.Warning(" margin of safe buy dynamicMarginOfSafe: {0} marginOfSafe: {1} bidAskSpread: {2}", dynamicMarginOfSafe, marginOfSafe, bidAskSpread);
                            // margin of safe to buy in the best price 0.02%
                            price = decimal.Round(marketPrice - marketPrice * (marginOfSafe / 100), 2);
                            marginOfSafe += 0.004m;
                        }
                    }

                    Log.Warning("buy Market: {0} new price: {1}", marketPrice, price);

                    decimal amount = Utils.TruncateDecimal(freeBalance / price, BinanceClient.DecimalAmount);

                    if (BinanceClient.TryPlaceOrder(OrderSide.Buy, OrderType.Limit, amount, price, TimeInForce.GoodTillCancel, out Order order))
                    {
                        Log.Warning("place buy at: {0}", price);
                    }
                }
            }
        }

        public override void SellLimit()
        {
            decimal percentage = Config.SellPercentage;
            decimal fee = 0.075m; //BNB fee

            Log.Warning("decimalsStep: {0}", BinanceClient.DecimalAmount);
            decimal amount = PlacedOrder.Quantity;
            if (PlacedOrder.Commission > 0)
            {
                amount = Utils.TruncateDecimal(PlacedOrder.Quantity - (PlacedOrder.Quantity * (fee / 100)), BinanceClient.DecimalAmount);//BNB fee
                Log.Warning("buy Commission sell at: {0}", PlacedOrder.Quantity * 0.00075m);
                percentage += fee;//recovery the fee
            }

            decimal newPrice = decimal.Round(PlacedOrder.Price + PlacedOrder.Price * (percentage / 100), 2);
            try
            {
                if (BinanceClient.TryPlaceOrder(OrderSide.Sell, OrderType.Limit, amount, newPrice, TimeInForce.GoodTillCancel, out Order order))
                {
                    Log.Warning("place sell at: {0}", newPrice);
                }
            }
            catch (Exception ex)
            {
                Log.Fatal("error sell price: {0} amount: {1} {2}", newPrice, amount, ex.Message);
            }
        }

    }
}
