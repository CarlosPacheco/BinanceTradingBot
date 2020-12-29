using Binance.Net.Enums;
using ImMillionaire.Brain.Core.Enums;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain.Core
{
    public abstract class BaseBot : IDisposable
    {
        protected IBinanceClient BinanceClient { get; set; }

        private ConfigOptions Configuration { get; }

        private AccountBinanceSymbol BinanceSymbol { get; set; }

        protected EventOrderBook OrderBook { get; set; }

        private long Ping { get; set; }

        protected Order PlacedOrder { get; set; }

        protected Dictionary<KlineInterval, IList<IOhlcv>> Candlesticks { get; set; } = new Dictionary<KlineInterval, IList<IOhlcv>>();

        public BaseBot(WalletType walletType)
        {
            /* Binance Configuration */
            Configuration = JsonConvert.DeserializeObject<ConfigOptions>(File.ReadAllText("config.json"));

            //binance client factory
            switch (walletType)
            {
                case WalletType.Spot:
                    BinanceClient = new BinanceClientSpot(Configuration);
                    break;
                case WalletType.Margin:
                    BinanceClient = new BinanceClientMargin(Configuration);
                    break;
                case WalletType.Futures:
                    BinanceClient = new BinanceClientFutures(Configuration);
                    break;
            }
        }

        public void Start()
        {
            Log.Information("Bot Starting host...");

            // Public
            Ping = BinanceClient.Ping();
            BinanceSymbol = BinanceClient.GetAccountBinanceSymbol();

            Log.Information("BinanceSymbol: {0}", BinanceSymbol.Name);

            // Candlesticks
            RegisterCandlestickUpdates();

            BinanceClient.StartSocketConnections(EventOrderBook, OrderUpdate);

            Log.Information("Bot end init");
        }

        protected void SubscribeCandlesticks(KlineInterval klineInterval, Action<IList<IOhlcv>, Candlestick> CandlestickUpdate)
        {
            IList<IOhlcv> Candlestick = BinanceClient.GetKlines(klineInterval);
            Candlesticks.Add(klineInterval, Candlestick);
            BinanceClient.SubscribeToKlineUpdates(Candlestick, klineInterval, CandlestickUpdate);
        }

        // protected abstract void KlineUpdates(IList<IOhlcv> candlestick, Candlestick candle);

        protected abstract void RegisterCandlestickUpdates();

        protected abstract void OrderUpdate(Order order);

        protected void EventOrderBook(EventOrderBook eventOrderBook) => OrderBook = eventOrderBook;

        public virtual void BuyLimit()
        {
            if (PlacedOrder == null)
            {
                decimal freeBalance = BinanceClient.GetFreeQuoteBalance();
                if (freeBalance > 1 && OrderBook != null)
                {
                    var locallastBid = OrderBook.LastBidPrice;

                    // margin of safe to buy in the best price 0.02%
                    decimal price = decimal.Round(locallastBid - locallastBid * (0.02m / 100), 2);
                    decimal amount = Utils.TruncateDecimal(freeBalance / price, BinanceClient.DecimalAmount);

                    if (BinanceClient.TryPlaceOrder(OrderSide.Buy, OrderType.Limit, amount, price, TimeInForce.GoodTillCancel, out Order order))
                    {
                        PlacedOrder = order;
                        CheckBuyWasExecuted();
                        Log.Warning("place buy at: {0}", price);
                    }
                }
            }
        }

        public virtual async void BuyLimitAsync()
        {

        }

        public virtual void SellLimit()
        {
            //var newPrice = decimal.Round(price + price * (0.15m / 100), 2);
            //try
            //{
            //    if(BinanceClient.TryPlaceOrder(OrderSide.Sell, OrderType.Limit, amount, newPrice, TimeInForce.GoodTillCancel, out Order order))
            //    {
            //        Utils.Log($"place sell at: {newPrice}", ConsoleColor.Green);
            //    }                        
            //}
            //catch (Exception ex)
            //{
            //    Utils.Log($"error place sell at: {newPrice} {ex.Message}", ConsoleColor.Red);
            //}
        }

        public virtual async void SellLimitAsync()
        {

        }

        protected void CheckBuyWasExecuted(int waitSecondsBeforeCancel = 60)
        {
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
            });
        }

        public void Dispose()
        {
            BinanceClient.Dispose();
        }
    }
}
