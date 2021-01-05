using Binance.Net.Enums;
using ImMillionaire.Brain.Core;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Generic;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain
{
    public class MyBotTradeConservative : BaseBot
    {
        decimal marketPrice;

        MyCandle MyCandle = new MyCandle();

        public MyBotTradeConservative(IBinanceClientFactory factory) : base(factory, Core.Enums.WalletType.Margin)
        {
        }

        protected override void RegisterCandlestickUpdates()
        {
            SubscribeCandlesticks(KlineInterval.OneMinute, KlineUpdates);
            SubscribeCandlesticks(KlineInterval.ThreeMinutes, (candlestick, candle) => MyCandle.SetThreeMinutes(candlestick));
            SubscribeCandlesticks(KlineInterval.OneHour, (candlestick, candle) => MyCandle.SetOneHour(candlestick));
        }

        protected /*override*/ void KlineUpdates(IList<IOhlcv> candlestick, Candlestick candle)
        {
            marketPrice = candle.Close;
          
            if (MyCandle.OneHourRsi14 > 70m)
            {
                if(PlacedOrder != null) SellLimit();
            }
            else if (MyCandle.OneHourRsi14 < 30m)
            {
                BuyLimit();
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
                    CheckBuyWasExecuted(60);
                }
                else if (order.Status == OrderStatus.Filled)
                {
                    PlacedOrder = order;                 
                }
                else if (order.Status == OrderStatus.Canceled)
                {
                    Log.Information("cancel buy");
                    PlacedOrder = null;
                }
            }
            else //OrderSide.Sell
            {
                if (order.Status == OrderStatus.Filled)
                {
                    Log.Information("sell");
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
                        if (price == marketPrice)
                        {
                            Log.Warning("place buy Market: {0} Bid: {1}", marketPrice, price);
                            price = decimal.Round(marketPrice - marketPrice * (0.016m / 100), 2);
                        }
                        
                        if (price > marketPrice)
                        {
                            Log.Warning("place buy Market: {0} Bid: {1}", marketPrice, price);

                            // margin of safe to buy in the best price 0.03%
                            price = decimal.Round(marketPrice - marketPrice * (0.022m / 100), 2);
                        }
                    }

                    Log.Warning("buy Market: {0} new price: {1}", marketPrice, price);
                    Log.Warning("decimalsStep: {0}", BinanceClient.DecimalAmount);
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
            decimal percentage = 0.15m;
            decimal fee = 0.075m; //BNB fee

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
