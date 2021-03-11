using Binance.Net.Enums;
using ImMillionaire.Brain.Models;
using ImMillionaire.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain.Core
{
    public abstract class BaseBot : IBotTrade, IDisposable
    {
        private readonly IBinanceClientFactory _factory;
        protected ILogger<BaseBot> Logger { get; }

        protected IBinanceClient BinanceClient { get; set; }

        protected EventOrderBook OrderBook { get; set; }

        private long Ping { get; set; }

        protected Order PlacedOrder { get; set; }

        protected Dictionary<KlineInterval, IList<IOhlcv>> Candlesticks { get; } = new Dictionary<KlineInterval, IList<IOhlcv>>();

        protected CancellationTokenSource TokenSourceBuyWasExecuted = new CancellationTokenSource();

        protected Bot Bot { get; set; }

        protected decimal MarketPrice { get; set; }

        public BaseBot(IBinanceClientFactory factory, ILogger<BaseBot> logger)
        {
            _factory = factory;
            Logger = logger;
        }

        public void Start(Bot bot)
        {
            Logger.LogInformation("Bot Starting host...");
            Bot = bot;
            BinanceClient = _factory.GetBinanceClient(Bot.Type);

            // Public
            Ping = BinanceClient.Ping();

            BinanceClient.StartSocketConnections(bot.Symbol, EventOrderBook, InternalOrderUpdate);

            // Candlesticks
            SubscribeCandlesticks(bot.KlineInterval, InternalKlineUpdates);
            RegisterCandlestickUpdates();

            Logger.LogInformation("Bot end init");
        }

        private void InternalOrderUpdate(Order order)
        {
            try
            {
                Logger.LogInformation("InternalOrderUpdate order: {@Order}", order);
                Logger.LogInformation("InternalOrderUpdate current PlacedOrder: {@PlacedOrder}", PlacedOrder);
                if (PlacedOrder == null || order.Status == OrderStatus.New || order.Symbol != Bot.Symbol || order.OrderId != PlacedOrder.OrderId) return;

                OrderUpdate(order);
            }
            catch (Exception ex)
            {
                Logger.LogError("InternalOrderUpdate {@ex}", ex);
            }
        }

        private void InternalKlineUpdates(IList<IOhlcv> candlesticks, Candlestick candle)
        {
            MarketPrice = candle.Close;
            KlineUpdates(candlesticks, candle);
        }

        protected void SubscribeCandlesticks(KlineInterval klineInterval, Action<IList<IOhlcv>, Candlestick> CandlestickUpdate)
        {
            IList<IOhlcv> Candlestick = BinanceClient.GetKlines(klineInterval);
            Candlesticks.Add(klineInterval, Candlestick);
            BinanceClient.SubscribeToKlineUpdates(Candlestick, klineInterval, CandlestickUpdate);
        }

        protected abstract void KlineUpdates(IList<IOhlcv> candlesticks, Candlestick candle);

        protected abstract void RegisterCandlestickUpdates();

        protected virtual void OrderUpdate(Order order)
        {
            Logger.LogInformation("Order {@Order}", order);
            // Handle order update info data
            if (order.Side == OrderSide.Buy)
            {
                switch (order.Status)
                {
                    case OrderStatus.PartiallyFilled:
                        if (TokenSourceBuyWasExecuted.IsCancellationRequested) break;
                        TokenSourceBuyWasExecuted.Cancel();
                        Logger.LogInformation("Cancel CheckBuyWasExecuted");
                        break;
                    case OrderStatus.Filled:
                        PlacedOrder = order;
                        if (Bot.PlaceSellWhenBuyFilled) SellLimit();
                        break;
                    case OrderStatus.Canceled:
                    case OrderStatus.PendingCancel:
                    case OrderStatus.Rejected:
                    case OrderStatus.Expired:
                        Logger.LogInformation("Cancel buy");
                        PlacedOrder = null;
                        break;
                    default:
                        break;
                }
            }
            else //OrderSide.Sell
            {
                switch (order.Status)
                {
                    case OrderStatus.Filled:
                        Logger.LogInformation("Sell");
                        PlacedOrder = null;
                        break;
                    case OrderStatus.Canceled:
                    case OrderStatus.PendingCancel:
                    case OrderStatus.Rejected:
                    case OrderStatus.Expired:
                        Logger.LogInformation("Cancel Sell");
                        PlacedOrder = null;
                        break;
                    default:
                        break;
                }
            }
        }

        protected void EventOrderBook(EventOrderBook eventOrderBook) => OrderBook = eventOrderBook;

        public virtual void BuyLimit()
        {
            if (PlacedOrder != null) return;

            decimal freeBalance = BinanceClient.GetFreeQuoteBalance();
            if (freeBalance <= 1 || OrderBook == null || freeBalance < BinanceClient.GetAccountBinanceSymbol().NotionalFilter.MinNotional) return;

            if (!Bot.UseAllAmount)
            {
                if (Bot.Amount <= 0) Bot.Amount = Bot.InitAmount + Bot.WonAmount;

                if (freeBalance >= Bot.Amount) freeBalance = Bot.Amount;
            }

            decimal price = OrderBook.LastBidPrice;

            if (MarketPrice > 0)
            {
                // margin of safe to buy in the best price 0.02%
                decimal marginOfSafe = Bot.BuyMarginOfSafe;
                while (price >= MarketPrice)
                {
                    decimal bidAskSpread = decimal.Round((OrderBook.LastAskPrice - OrderBook.LastBidPrice) / OrderBook.LastAskPrice * 100, 3);
                    decimal dynamicMarginOfSafe = decimal.Round(price * 100 / MarketPrice - 100, 3);
                    Logger.LogWarning("place buy Market: {0} Bid: {1}", MarketPrice, price);
                    if (dynamicMarginOfSafe > marginOfSafe) marginOfSafe = dynamicMarginOfSafe;
                    Logger.LogWarning(" margin of safe buy dynamicMarginOfSafe: {0} marginOfSafe: {1} bidAskSpread: {2}", dynamicMarginOfSafe, marginOfSafe, bidAskSpread);
                    // margin of safe to buy in the best price 0.02%
                    price = MarketPrice * (1m - marginOfSafe / 100);
                    marginOfSafe += 0.004m;
                }
            }

            Logger.LogWarning("buy Market: {0} new price: {1}", MarketPrice, price);

            decimal quantity = freeBalance / price;
            if (BinanceClient.TryPlaceOrder(OrderSide.Buy, OrderType.Limit, quantity, price, TimeInForce.GoodTillCancel, out Order order))
            {
                PlacedOrder = order;
                CheckBuyWasExecuted(Bot.WaitSecondsBeforeCancelOrder);
                Logger.LogWarning("place buy at: {0}", order.Price);
            }
        }

        public virtual void SellLimit()
        {
            if (PlacedOrder == null) return;

            decimal percentage = Bot.SellMarginOfSafe;
            decimal fee = 0.075m; //BNB fee

            decimal quantity = PlacedOrder.Quantity;
            if (PlacedOrder.Commission > 0)
            {
                quantity = PlacedOrder.Quantity * (1m - fee / 100);//BNB fee
                Logger.LogWarning("buy Commission sell at: {0} {@PlacedOrder}", PlacedOrder.Quantity * (fee / 100), PlacedOrder);
                percentage += fee;//recovery the fee
            }

            decimal newPrice = PlacedOrder.Price * (1m + percentage / 100);
            if (BinanceClient.TryPlaceOrder(OrderSide.Sell, OrderType.Limit, quantity, newPrice, TimeInForce.GoodTillCancel, out Order order))
            {
                PlacedOrder = order;
                Logger.LogWarning("place sell at: {0}", order.Price);
                if (!TokenSourceBuyWasExecuted.IsCancellationRequested) TokenSourceBuyWasExecuted.Cancel();
            }
        }

        protected void CheckBuyWasExecuted(int waitSecondsBeforeCancel = 60)
        {
            if (TokenSourceBuyWasExecuted.IsCancellationRequested)
            {
                TokenSourceBuyWasExecuted.Dispose();
                TokenSourceBuyWasExecuted = new CancellationTokenSource();
            }

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(waitSecondsBeforeCancel), TokenSourceBuyWasExecuted.Token);
                    if (PlacedOrder == null || TokenSourceBuyWasExecuted.IsCancellationRequested) return;

                    if (BinanceClient.TryGetOrder(PlacedOrder.OrderId, out Order order))
                    {
                        if (order.Status == OrderStatus.New)
                        {
                            await BinanceClient.CancelOrderAsync(PlacedOrder.OrderId);
                        }
                    }
                }
                catch (Exception)
                {
                }
            }, TokenSourceBuyWasExecuted.Token);
        }

        public void Dispose()
        {
            TokenSourceBuyWasExecuted?.Dispose();
            BinanceClient.Dispose();
        }
    }
}
