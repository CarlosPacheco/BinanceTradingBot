using Binance.Net.Enums;
using CryptoExchange.Net;
using ImMillionaire.Brain.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Trady.Analysis.Extension;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain
{
    public class MyBotTrade : BaseBot
    {
        decimal marketPrice;

        public MyBotTrade() : base(Core.Enums.WalletType.Margin)
        {
        }

        protected override void KlineUpdates(IList<IOhlcv> candlestick, Candlestick candle)
        {
            marketPrice = candle.Close;
            // Utils.Log($"market price: {marketPrice}", ConsoleColor.DarkYellow);

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
            // Handle order update info data
            if (order.Side == OrderSide.Buy)
            {
                if (order.Status == OrderStatus.New)
                {
                    PlacedOrder = order;
                    CheckBuyWasExecuted(25);
                }
                else if (order.Status == OrderStatus.Filled)
                {
                    PlacedOrder = order;
                    SellLimit();
                }
                else if (order.Status == OrderStatus.Canceled)
                {
                    Utils.Log($"cancel buy", ConsoleColor.Green);
                    PlacedOrder = null;
                }
            }
            else //OrderSide.Sell
            {
                if (order.Status == OrderStatus.Filled)
                {
                    Utils.Log($"sell", ConsoleColor.Red);
                    PlacedOrder = null;
                }
            }
        }
        public override void BuyLimit()
        {
            if (PlacedOrder == null)
            {
                decimal freeBalance = BinanceClient.GetFreeBalance();
                if (freeBalance > 1)
                {
                    decimal price = OrderBook.LastBidPrice;

                    if (marketPrice > 0)
                    {
                        if (price >= marketPrice)
                        {
                            Utils.Log($"place buy Market: {marketPrice} Bid: {price}", ConsoleColor.DarkMagenta);

                            // margin of safe to buy in the best price 0.02%
                            price = decimal.Round(marketPrice - marketPrice * (0.016m / 100), 2);
                        }
                    }

                    Utils.Log($"buy Market: {marketPrice} new price: {price}", ConsoleColor.DarkRed);
                    var amount = Utils.TruncateDecimal(freeBalance / price, 6);

                    if (BinanceClient.TryPlaceOrder(OrderSide.Buy, OrderType.Limit, amount, price, TimeInForce.GoodTillCancel, out Order order))
                    {
                        Utils.Log($"place buy at: {price}", ConsoleColor.Green);
                    }
                }
            }
        }

        public override void SellLimit()
        {
            decimal percentage = 0.15m;

            decimal amount = PlacedOrder.Quantity;
            if (PlacedOrder.Commission > 0)
            {
                amount = Utils.TruncateDecimal(PlacedOrder.Quantity - (PlacedOrder.Quantity * 0.00075m), 6);//BNB fee
                Utils.Log($"buy Commission sell at: {(PlacedOrder.Quantity * 0.00075m)}", ConsoleColor.DarkRed);
                percentage += 0.075m;//recovery the fee
            }

            var newPrice = decimal.Round(PlacedOrder.Price + PlacedOrder.Price * (percentage / 100), 2);
            try
            {
                if (BinanceClient.TryPlaceOrder(OrderSide.Sell, OrderType.Limit, amount, newPrice, TimeInForce.GoodTillCancel, out Order order))
                {
                    Utils.Log($"place sell at: {newPrice}", ConsoleColor.Green);
                }
            }
            catch (Exception ex)
            {
                Utils.Log($"error sell price: {newPrice} amount: {amount} {ex.Message}", ConsoleColor.Red);
            }
        }
    }
}
