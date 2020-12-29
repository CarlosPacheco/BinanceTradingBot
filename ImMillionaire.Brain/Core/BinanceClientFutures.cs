using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.SocketSubClient;
using Binance.Net.Interfaces.SubClients;
using Binance.Net.Objects.Futures.FuturesData;
using Binance.Net.Objects.Futures.MarketData;
using Binance.Net.Objects.Futures.MarketStream;
using Binance.Net.Objects.Futures.UserStream;
using Binance.Net.Objects.Spot;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain.Core
{
    public class BinanceClientFutures : IBinanceClient
    {
        private IBinanceSocketClient SocketClient { get; set; }

        private Binance.Net.Interfaces.IBinanceClient Client { get; set; }

        private string listenKey;

        public ConfigOptions Configuration { get; }

        private AccountBinanceSymbol BinanceSymbol { get; }

        public decimal GetCurrentTradePrice { get; set; }

        public IBinanceClientMarket Market { get; set; }

        public IBinanceClientUserStream UserStream { get; set; }

        public IBinanceSocketClientBase BinanceSocketClientBase { get; set; }

        public int DecimalAmount { get; set; }

        public BinanceClientFutures(ConfigOptions config)
        {
            Configuration = config;

            SocketClient = new BinanceSocketClient(new BinanceSocketClientOptions()
            {
                ApiCredentials = new ApiCredentials(config.ApiKey, config.SecretKey)
            });
            Client = new BinanceClient(new BinanceClientOptions()
            {
                ApiCredentials = new ApiCredentials(config.ApiKey, config.SecretKey)
            });

            BinanceFuturesSymbol symbol = Client.FuturesUsdt.System.GetExchangeInfo().Data.Symbols.FirstOrDefault(x => x.Name == config.Symbol);
            if (symbol == null) throw new Exception("Symbol don't exist!");
            BinanceSymbol = new AccountBinanceSymbol(symbol);

            decimal stepSize = symbol.LotSizeFilter.StepSize;
            if (stepSize != 0.0m)
            {
                for (DecimalAmount = 0; stepSize < 1; DecimalAmount++)
                {
                    stepSize *= 10;
                }
            }

            Market = Client.FuturesUsdt.Market;
            UserStream = Client.FuturesUsdt.UserStream;
            BinanceSocketClientBase = SocketClient.FuturesUsdt;
        }

        public IList<IOhlcv> GetKlines(KlineInterval klineInterval)
        {
            WebCallResult<IEnumerable<IBinanceKline>> klines = Market.GetKlines(BinanceSymbol.Name, klineInterval);
            if (klines.Success)
            {
                return klines.Data.Select(k => new Candlestick(k)).ToList<IOhlcv>();
            }
            else
            {
                Log.Fatal("{0}", klines.Error?.Message);
            }

            return null;
        }

        public long Ping()
        {
            return Client.Ping().Data;
        }

        public AccountBinanceSymbol GetAccountBinanceSymbol()
        {
            return BinanceSymbol;
        }

        public bool TryPlaceOrder(OrderSide side, OrderType type, decimal quantity, decimal price, TimeInForce timeInForce, out Order order)
        {
            order = null;
            WebCallResult<BinanceFuturesPlacedOrder> orderRequest = Client.FuturesUsdt.Order.PlaceOrder(BinanceSymbol.Name, side, type, quantity, PositionSide.Both, timeInForce, null, price);
            if (!orderRequest.Success)
            {
                Log.Fatal("{0}", orderRequest.Error?.Message);
                return false;
            }

            order = new Order(orderRequest.Data);
            return true;
        }

        public bool TryGetOrder(long orderId, out Order order)
        {
            order = null;
            WebCallResult<BinanceFuturesOrder> orderRequest = Client.FuturesUsdt.Order.GetOrder(BinanceSymbol.Name, orderId);
            if (!orderRequest.Success)
            {
                Log.Fatal("{0}", orderRequest.Error?.Message);
                return false;
            }

            order = new Order(orderRequest.Data);
            return true;
        }

        public bool CancelOrder(long orderId)
        {
            return Client.FuturesUsdt.Order.CancelOrder(BinanceSymbol.Name, orderId).Success;
        }

        public async Task<bool> CancelOrderAsync(long orderId)
        {
            return (await Client.FuturesUsdt.Order.CancelOrderAsync(BinanceSymbol.Name, orderId)).Success;
        }

        public async Task<(bool IsSucess, Order Order)> TryPlaceOrderAsync(OrderSide side, OrderType type, decimal quantity, decimal price, TimeInForce timeInForce)
        {
            WebCallResult<BinanceFuturesPlacedOrder> orderRequest = await Client.FuturesUsdt.Order.PlaceOrderAsync(BinanceSymbol.Name, side, type, quantity, PositionSide.Both, timeInForce, null, price);
            if (!orderRequest.Success)
            {
                Log.Warning($"error place {side} at: {price}");
                Log.Fatal("{0}", orderRequest.Error?.Message);
                return (false, null);
            }

            return (true, new Order(orderRequest.Data));
        }

        public void SubscribeToKlineUpdates(IList<IOhlcv> candlestick, KlineInterval interval, Action<IList<IOhlcv>, Candlestick> calculateIndicators)
        {
            CallResult<UpdateSubscription> successKline = BinanceSocketClientBase.SubscribeToKlineUpdates(BinanceSymbol.Name, interval, (IBinanceStreamKlineData data) =>
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

        public void StartSocketConnections(Action<EventOrderBook> eventOrderBook, Action<Order> orderUpdate)
        {
            SubscribeToBookTickerUpdates(eventOrderBook);
            GetListenKey();
            SubscribeToUserDataUpdates(orderUpdate);
            KeepAliveListenKey();
        }

        private void SubscribeToUserDataUpdates(Action<Order> orderUpdate)
        {
            if (string.IsNullOrWhiteSpace(listenKey))
            {
                Log.Fatal("ListenKey can't be null, maybe you have Api key Restrict access to trusted IPs only enabled");
                GetListenKey();
            }
            CallResult<UpdateSubscription> successAccount = SocketClient.FuturesUsdt.SubscribeToUserDataUpdates(listenKey,
            null, // onMarginUpdate
            null, // onAccountUpdate      
            (BinanceFuturesStreamOrderUpdate data) => orderUpdate(new Order(data.UpdateData)), // onOrderUpdate
            null); // onListenKeyExpired

            UpdateSubscriptionAutoConnetionIfConnLost(successAccount, () => SubscribeToUserDataUpdates(orderUpdate));
        }

        private void SubscribeToBookTickerUpdates(Action<EventOrderBook> eventOrderBook)
        {
            CallResult<UpdateSubscription> success = SocketClient.FuturesUsdt.SubscribeToBookTickerUpdates(BinanceSymbol.Name, (BinanceFuturesStreamBookPrice data) => eventOrderBook(new EventOrderBook(data.BestAskPrice, data.BestBidPrice)));
            UpdateSubscriptionAutoConnetionIfConnLost(success, () => SubscribeToBookTickerUpdates(eventOrderBook));
        }

        private void GetListenKey()
        {
            WebCallResult<string> result = UserStream.StartUserStream();
            if (result.Success)
            {
                listenKey = result.Data;
            }
            else
            {
                Log.Fatal("{0} - GetListenKey", result.Error?.Message);
            }
        }

        private async void KeepAliveListenKey()
        {
            while (true)
            {
                await Task.Delay(new TimeSpan(0, 45, 0));

                if (!UserStream.KeepAliveUserStream(listenKey).Success)
                {
                    listenKey = UserStream.StartUserStream().Data;
                }
            }
        }

        private void UpdateSubscriptionAutoConnetionIfConnLost(CallResult<UpdateSubscription> updateSubscription, Action callback)
        {
            if (updateSubscription.Success)
            {
                updateSubscription.Data.ConnectionLost += () =>
                {
                    //SocketClient.Unsubscribe(updateSubscription.Data);
                    //callback();
                    Log.Fatal("ConnectionLost {0}", updateSubscription.Error?.Message);
                };

                updateSubscription.Data.Exception += (ex) =>
                {
                    Log.Fatal(ex, "ConnectionLost error unexpectedly.");
                };
            }
            else
            {
                Log.Fatal("{0}", updateSubscription.Error?.Message);
            }
        }

        public decimal GetFreeBaseBalance()
        {
            return GetFreeBalance(BinanceSymbol.BaseAsset);
        }

        public decimal GetFreeQuoteBalance()
        {
            return GetFreeBalance(BinanceSymbol.QuoteAsset);
        }

        public decimal GetFreeBalance(string asset)
        {
            WebCallResult<BinanceFuturesAccountInfo> binanceAccount = Client.FuturesUsdt.Account.GetAccountInfo();
            if (binanceAccount.Success)
            {
                var currentTradingAsset = binanceAccount.Data.Assets.FirstOrDefault(x => x.Asset.ToLower() == asset.ToLower());
                var currentTradingPosition = binanceAccount.Data.Positions.FirstOrDefault(x => x.Symbol == BinanceSymbol.Name);
                return currentTradingAsset.MaxWithdrawAmount * currentTradingPosition.Leverage;
            }
            else
            {
                Log.Fatal("{0}", binanceAccount.Error?.Message);
            }

            return 0;
        }

        public void Dispose()
        {
            if (!string.IsNullOrWhiteSpace(listenKey)) UserStream.StopUserStream(listenKey);
            Client?.Dispose();
            SocketClient?.UnsubscribeAll();
            SocketClient?.Dispose();
        }
    }
}
