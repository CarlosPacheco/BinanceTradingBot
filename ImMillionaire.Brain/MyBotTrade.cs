﻿using Binance.Net.Enums;
using ImMillionaire.Brain.Core;
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

        //MyCandle MyCandle = new MyCandle();

        public MyBotTrade() : base(Core.Enums.WalletType.Margin)
        {
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
            // Handle order update info data
            if (order.Side == OrderSide.Buy)
            {
                if (order.Status == OrderStatus.New)
                {
                    PlacedOrder = order;
                    CheckBuyWasExecuted(40);
                }
                else if (order.Status == OrderStatus.Filled)
                {
                    PlacedOrder = order;
                    SellLimit();
                }
                else if (order.Status == OrderStatus.Canceled)
                {
                    Log.Information($"cancel buy");
                    PlacedOrder = null;
                }
            }
            else //OrderSide.Sell
            {
                if (order.Status == OrderStatus.Filled)
                {
                    Log.Information($"sell");
                    PlacedOrder = null;
                }
            }
        }
        public override void BuyLimit()
        {
            if (PlacedOrder == null)
            {
                decimal freeBalance = BinanceClient.GetFreeQuoteBalance();
                if (freeBalance > 1)
                {
                    decimal price = OrderBook.LastBidPrice;

                    if (marketPrice > 0)
                    {
                        if (price >= marketPrice)
                        {
                            Log.Information($"place buy Market: {marketPrice} Bid: {price}");

                            // margin of safe to buy in the best price 0.02%
                            price = decimal.Round(marketPrice - marketPrice * (0.016m / 100), 2);
                        }

                        if (price > marketPrice)
                        {
                            Log.Information($"place buy Market: {marketPrice} Bid: {price}");

                            // margin of safe to buy in the best price 0.03%
                            price = decimal.Round(marketPrice - marketPrice * (0.022m / 100), 2);
                        }
                    }

                    Log.Information($"buy Market: {marketPrice} new price: {price}");
                    AccountBinanceSymbol account = BinanceClient.GetAccountBinanceSymbol();
                    decimal stepSize = account.LotSizeFilter.StepSize;
                    int decimalsStep = 0;
                    if (stepSize != 0.0m)
                    {
                        for (decimalsStep = -1; stepSize < 1; decimalsStep++)
                        {
                            stepSize *= 10;
                        }
                    }
                    Log.Information($"decimalsStep: {decimalsStep}");
                    var amount = Utils.TruncateDecimal(freeBalance / price, 5);

                    if (BinanceClient.TryPlaceOrder(OrderSide.Buy, OrderType.Limit, amount, price, TimeInForce.GoodTillCancel, out Order order))
                    {
                        Log.Information($"place buy at: {price}");
                    }
                }
            }
        }

        public override void SellLimit()
        {
            decimal percentage = 0.25m;
            decimal fee = 0.075m; //BNB fee

            AccountBinanceSymbol account = BinanceClient.GetAccountBinanceSymbol();
            decimal stepSize = account.LotSizeFilter.StepSize;
            int decimalsStep = 0;
            if (stepSize != 0.0m)
            {
                for (decimalsStep = -1; stepSize < 1; decimalsStep++)
                {
                    stepSize *= 10;
                }
            }
            Log.Information($"decimalsStep: {decimalsStep}");
            decimal amount = PlacedOrder.Quantity;
            if (PlacedOrder.Commission > 0)
            {
                amount = Utils.TruncateDecimal(PlacedOrder.Quantity - (PlacedOrder.Quantity * (fee / 100)), decimalsStep);//BNB fee
                Log.Information($"buy Commission sell at: {(PlacedOrder.Quantity * 0.00075m)}");
                percentage += fee;//recovery the fee
            }

            var newPrice = decimal.Round(PlacedOrder.Price + PlacedOrder.Price * (percentage / 100), 2);
            try
            {
                if (BinanceClient.TryPlaceOrder(OrderSide.Sell, OrderType.Limit, amount, newPrice, TimeInForce.GoodTillCancel, out Order order))
                {
                    Log.Information($"place sell at: {newPrice}");
                }
            }
            catch (Exception ex)
            {
                Log.Information($"error sell price: {newPrice} amount: {amount} {ex.Message}");
            }
        }

    }
}
