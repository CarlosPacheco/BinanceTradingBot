using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot;
using Binance.Net.Objects.Spot.MarginData;
using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.Objects.Spot.MarketStream;
using Binance.Net.Objects.Spot.UserStream;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain.Core
{
    public class BinanceClientMargin : IBinanceClient
    {
        private IBinanceSocketClient SocketClient { get; set; }

        private Binance.Net.Interfaces.IBinanceClient Client { get; set; }

        private string Symbol { get; set; }

        private string listenKey;

        public ConfigOptions Configuration { get; }
        public decimal GetCurrentTradePrice { get; set; }

        public BinanceClientMargin(ConfigOptions config)
        {
            Configuration = config;
            Symbol = config.Symbol;

            BinanceClient.SetDefaultOptions(new BinanceClientOptions()
            {
                ApiCredentials = new ApiCredentials(config.ApiKey, config.SecretKey)
            });
            BinanceSocketClient.SetDefaultOptions(new BinanceSocketClientOptions()
            {
                ApiCredentials = new ApiCredentials(config.ApiKey, config.SecretKey)
            });

            SocketClient = new BinanceSocketClient();
            Client = new BinanceClient();
        }

        public IList<IOhlcv> GetKlines(KlineInterval klineInterval)
        {
            WebCallResult<IEnumerable<BinanceKline>> klines = Client.GetKlines(Symbol, klineInterval);
            if (klines.Success)
            {
                return klines.Data.Select(k => new Candlestick(k)).ToList<IOhlcv>();
            }

            return null;
        }

        public long Ping()
        {
            return Client.Ping().Data;
        }

        public AccountBinanceSymbol GetAccountBinanceSymbol()
        {
            return new AccountBinanceSymbol(Client.GetExchangeInfo().Data.Symbols.FirstOrDefault(x => x.Name == Symbol));
        }

        public bool TryPlaceOrder(OrderSide side, OrderType type, decimal quantity, decimal price, TimeInForce timeInForce, out Order order)
        {
            order = null;
            var orderRequest = Client.PlaceMarginOrder(Symbol, side, type, quantity, null, null, price, timeInForce);
            if (!orderRequest.Success)
            {
                Utils.Log($"error place {side} at: {price} {orderRequest.Error.Message}", ConsoleColor.Red);
                return false;
            }

            order = new Order(orderRequest.Data);
            return true;
        }

        public async Task<(bool IsSucess, Order Order)> TryPlaceOrderAsync(OrderSide side, OrderType type, decimal quantity, decimal price, TimeInForce timeInForce)
        {
            var orderRequest = await Client.PlaceMarginOrderAsync(Symbol, side, type, quantity, null, null, price, timeInForce);
            if (!orderRequest.Success)
            {
                Utils.Log($"error place {side} at: {price} {orderRequest.Error.Message}", ConsoleColor.Red);
                return (false, null);
            }

            return (true, new Order(orderRequest.Data));
        }

        public bool TryGetOrder(long orderId, out Order order)
        {
            order = null;

            var orderRequest = Client.GetMarginAccountOrder(Symbol, orderId);
            if (!orderRequest.Success) return false;

            order = new Order(orderRequest.Data);
            return true;
        }

        public bool CancelOrder(long orderId)
        {
            return Client.CancelMarginOrder(Symbol, orderId).Success;
        }

        public async Task<bool> CancelOrderAsync(long orderId)
        {
            return (await Client.CancelMarginOrderAsync(Symbol, orderId)).Success;
        }

        public void StartSocketConnections(Action<EventOrderBook> eventOrderBook, Action<Order> orderUpdate)
        {
           // var tardeFee = Client.GetTradeFee(Symbol).Data;
            //var account = Client.GetMarginAccountInfo().Data;

            SubscribeToOrderBookUpdates(eventOrderBook);
            GetListenKey();
            SubscribeToUserDataUpdates(orderUpdate);
        }

        private void SubscribeToUserDataUpdates(Action<Order> orderUpdate)
        {
            CallResult<UpdateSubscription> successAccount = SocketClient.SubscribeToUserDataUpdates(listenKey,
            null,// Handle account info data
            (BinanceStreamOrderUpdate data) =>
            {
                // Handle order update info data
                orderUpdate(new Order(data));
            },
            null, // Handler for OCO updates
            null, // Handler for position updates
            null); // Handler for account balance updates (withdrawals/deposits)

            UpdateSubscriptionAutoConnetionIfConnLost(successAccount, () => SubscribeToUserDataUpdates(orderUpdate));
        }

        private void SubscribeToOrderBookUpdates(Action<EventOrderBook> eventOrderBook)
        {
            CallResult<UpdateSubscription> successDepth = SocketClient.SubscribeToOrderBookUpdates(Symbol, 1000, (BinanceEventOrderBook data) =>
            {
                if (data.Asks.Any() && data.Bids.Any())
                {
                    eventOrderBook(new EventOrderBook(data.Asks.First().Price, data.Bids.First().Price));
                }
            });

            UpdateSubscriptionAutoConnetionIfConnLost(successDepth, () => SubscribeToOrderBookUpdates(eventOrderBook));
        }

        private async void GetListenKey()
        {
            WebCallResult<string> result = Client.StartMarginUserStream();
            if (result.Success)
            {
                listenKey = result.Data;

                while (true)
                {
                    await Task.Delay(new TimeSpan(0, 45, 0));

                    if (!Client.KeepAliveMarginUserStream(listenKey).Success)
                    {
                        listenKey = Client.StartMarginUserStream().Data;
                    }
                }
            }
        }

        public void SubscribeToKlineUpdates(IList<IOhlcv> candlestick, KlineInterval interval, Action<IList<IOhlcv>, Candlestick> calculateIndicators)
        {
            CallResult<UpdateSubscription> successKline = SocketClient.SubscribeToKlineUpdates(Symbol, interval, (BinanceStreamKlineData data) =>
            {
                Candlestick candle = new Candlestick(data.Data);
                candlestick.Add(candle);

                calculateIndicators(candlestick, candle);

                if (!data.Data.Final)
                {
                    candlestick.Remove(candle);
                }
            });

            UpdateSubscriptionAutoConnetionIfConnLost(successKline, () => SubscribeToKlineUpdates(candlestick, interval, calculateIndicators));
        }

        private void UpdateSubscriptionAutoConnetionIfConnLost(CallResult<UpdateSubscription> updateSubscription, Action callback)
        {
            if (updateSubscription.Success)
            {
                updateSubscription.Data.ConnectionLost += () =>
                {
                    SocketClient.Unsubscribe(updateSubscription.Data);
                    callback();
                    Utils.ErrorLog("ConnectionLost");
                  //  throw new Exception("restart all");
                };
            }
        }

        public decimal GetFreeBalance()
        {
            WebCallResult<BinanceMarginAccount> binanceMarginAccount = Client.GetMarginAccountInfo();
            if (binanceMarginAccount.Success)
            {
                var currentTradingAsset = binanceMarginAccount.Data.Balances.FirstOrDefault(x => x.Asset.ToLower() == Configuration.Trade.ToLower());
                return currentTradingAsset.Free;
            }

            return 0;
        }

        public void Dispose()
        {
            Client.CloseMarginUserStream(listenKey);
            Client?.Dispose();
            SocketClient?.UnsubscribeAll();
            SocketClient?.Dispose();
        }
    }
}
