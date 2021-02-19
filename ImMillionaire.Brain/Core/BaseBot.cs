using Binance.Net.Enums;
using ImMillionaire.Brain.Models;
using Serilog;
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
        protected ILogger Logger { get; }

        protected IBinanceClient BinanceClient { get; set; }

        protected EventOrderBook OrderBook { get; set; }

        private long Ping { get; set; }

        protected Order PlacedOrder { get; set; }

        protected Dictionary<KlineInterval, IList<IOhlcv>> Candlesticks { get; } = new Dictionary<KlineInterval, IList<IOhlcv>>();

        protected CancellationTokenSource tokenSource = new CancellationTokenSource();

        protected Bot Bot { get; set; }

        protected decimal MarketPrice { get; set; }

        public BaseBot(IBinanceClientFactory factory, ILogger logger)
        {
            _factory = factory;
            Logger = logger;
        }

        public void Start(Bot bot)
        {
            Logger.Information("Bot Starting host...");
            Bot = bot;
            BinanceClient = _factory.GetBinanceClient(Bot.Type);

            // Public
            Ping = BinanceClient.Ping();

            BinanceClient.StartSocketConnections(bot.Symbol, EventOrderBook, InternalOrderUpdate);

            // Candlesticks
            SubscribeCandlesticks(KlineInterval.OneMinute, InternalKlineUpdates);
            RegisterCandlestickUpdates();

            Logger.Information("Bot end init");
        }

        private void InternalOrderUpdate(Order order)
        {
            if (PlacedOrder == null || order.Symbol != Bot.Symbol || order.OrderId != PlacedOrder.OrderId) return;

            OrderUpdate(order);
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

        protected abstract void OrderUpdate(Order order);

        protected void EventOrderBook(EventOrderBook eventOrderBook) => OrderBook = eventOrderBook;

        public virtual void BuyLimit()
        {
            if (PlacedOrder != null) return;

            decimal freeBalance = BinanceClient.GetFreeQuoteBalance();
            if (freeBalance <= 1 || OrderBook == null) return;

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
                    Logger.Warning("place buy Market: {0} Bid: {1}", MarketPrice, price);
                    if (dynamicMarginOfSafe > marginOfSafe) marginOfSafe = dynamicMarginOfSafe;
                    Logger.Warning(" margin of safe buy dynamicMarginOfSafe: {0} marginOfSafe: {1} bidAskSpread: {2}", dynamicMarginOfSafe, marginOfSafe, bidAskSpread);
                    // margin of safe to buy in the best price 0.02%
                    price = decimal.Round(MarketPrice - MarketPrice * (marginOfSafe / 100), BinanceClient.DecimalPrice);
                    marginOfSafe += 0.004m;
                }
            }

            Logger.Warning("buy Market: {0} new price: {1}", MarketPrice, price);

            decimal quantity = Utils.TruncateDecimal(freeBalance / price, BinanceClient.DecimalQuantity);
            if (BinanceClient.TryPlaceOrder(OrderSide.Buy, OrderType.Limit, quantity, price, TimeInForce.GoodTillCancel, out Order order))
            {
                PlacedOrder = order;
                Logger.Warning("place buy at: {0}", price);
            }
        }

        public virtual void SellLimit()
        {
            decimal percentage = Bot.SellMarginOfSafe;
            decimal fee = 0.075m; //BNB fee

            Logger.Warning("decimalsStep: {0}", BinanceClient.DecimalQuantity);
            decimal quantity = PlacedOrder.Quantity;
            if (PlacedOrder.Commission > 0)
            {
                quantity = Utils.TruncateDecimal(PlacedOrder.Quantity - (PlacedOrder.Quantity * (fee / 100)), BinanceClient.DecimalQuantity);//BNB fee
                Logger.Warning("buy Commission sell at: {0} {@PlacedOrder}", PlacedOrder.Quantity * (fee / 100), PlacedOrder);
                percentage += fee;//recovery the fee
            }

            decimal newPrice = decimal.Round(PlacedOrder.Price + PlacedOrder.Price * (percentage / 100), BinanceClient.DecimalPrice);
            if (BinanceClient.TryPlaceOrder(OrderSide.Sell, OrderType.Limit, quantity, newPrice, TimeInForce.GoodTillCancel, out Order order))
            {
                PlacedOrder = order;
                Logger.Warning("place sell at: {0}", newPrice);
            }
        }

        public virtual async void BuyLimitAsync()
        {
        }

        public virtual async void SellLimitAsync()
        {
        }

        protected void CheckBuyWasExecuted(int waitSecondsBeforeCancel = 60)
        {
            if (tokenSource.IsCancellationRequested)
            {
                tokenSource.Dispose();
                tokenSource = new CancellationTokenSource();
            }

            Task.Run(() =>
            {
                Thread.Sleep(TimeSpan.FromSeconds(waitSecondsBeforeCancel));
                if (PlacedOrder == null) return;

                if (BinanceClient.TryGetOrder(PlacedOrder.OrderId, out Order order))
                {
                    if (order.Side == OrderSide.Buy && order.Status != OrderStatus.Filled)
                    {
                        BinanceClient.CancelOrderAsync(PlacedOrder.OrderId);
                    }
                }
            }, tokenSource.Token);
        }

        public void Dispose()
        {
            tokenSource?.Dispose();
            BinanceClient.Dispose();
        }
    }
}
