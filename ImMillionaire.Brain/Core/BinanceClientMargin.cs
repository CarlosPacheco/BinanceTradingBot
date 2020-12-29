using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.SocketSubClient;
using Binance.Net.Interfaces.SubClients;
using Binance.Net.Objects.Spot;
using Binance.Net.Objects.Spot.MarginData;
using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.Objects.Spot.SpotData;
using Binance.Net.Objects.Spot.UserStream;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using ImMillionaire.Brain.Logger;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain.Core
{
    public class BinanceClientMargin : IBinanceClient
    {
        private IBinanceSocketClient SocketClient { get; set; }

        private Binance.Net.Interfaces.IBinanceClient Client { get; set; }

        private string listenKey;

        public ConfigOptions Configuration { get; }
        public decimal GetCurrentTradePrice { get; set; }

        private AccountBinanceSymbol BinanceSymbol { get; }

        public IBinanceClientMarket Market { get; set; }

        public IBinanceClientUserStream UserStream { get; set; }

        public IBinanceSocketClientBase BinanceSocketClientBase { get; set; }

        public int DecimalAmount { get; set; }

        public BinanceClientMargin(ConfigOptions config)
        {
            Configuration = config;

            SocketClient = new BinanceSocketClient(new BinanceSocketClientOptions()
            {
                ApiCredentials = new ApiCredentials(config.ApiKey, config.SecretKey),
                SocketNoDataTimeout = TimeSpan.FromMinutes(5),
               // LogVerbosity = CryptoExchange.Net.Logging.LogVerbosity.Debug,
                LogWriters = new List<TextWriter> { TextWriterLogger.Out }
             
            });
            Client = new BinanceClient(new BinanceClientOptions()
            {
                ApiCredentials = new ApiCredentials(config.ApiKey, config.SecretKey),
                LogWriters = new List<TextWriter> { TextWriterLogger.Out }
                //LogVerbosity = CryptoExchange.Net.Logging.LogVerbosity.Debug,
                //AutoTimestamp = true,
                //AutoTimestampRecalculationInterval = TimeSpan.FromMinutes(30),
            });

            BinanceSymbol symbol = Client.Spot.System.GetExchangeInfo().Data.Symbols.FirstOrDefault(x => x.Name == config.Symbol);
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

            Market = Client.Spot.Market;
            UserStream = Client.Margin.UserStream;
            BinanceSocketClientBase = SocketClient.Spot;
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
            WebCallResult<BinancePlacedOrder> orderRequest = Client.Margin.Order.PlaceMarginOrder(BinanceSymbol.Name, side, type, quantity, null, null, price, timeInForce);
            if (!orderRequest.Success)
            {
                Log.Information("error place {0} at: {1}", side, price);
                Log.Fatal("{0}", orderRequest.Error?.Message);
                return false;
            }

            order = new Order(orderRequest.Data);
            return true;
        }

        public async Task<(bool IsSucess, Order Order)> TryPlaceOrderAsync(OrderSide side, OrderType type, decimal quantity, decimal price, TimeInForce timeInForce)
        {
            WebCallResult<BinancePlacedOrder> orderRequest = await Client.Margin.Order.PlaceMarginOrderAsync(BinanceSymbol.Name, side, type, quantity, null, null, price, timeInForce);
            if (!orderRequest.Success)
            {
                Log.Information("error place {0} at: {1}", side, price);
                Log.Fatal("{0}", orderRequest.Error?.Message);
                return (false, null);
            }

            return (true, new Order(orderRequest.Data));
        }

        public bool TryGetOrder(long orderId, out Order order)
        {
            order = null;
            WebCallResult<BinanceOrder> orderRequest = Client.Margin.Order.GetMarginAccountOrder(BinanceSymbol.Name, orderId);
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
            return Client.Margin.Order.CancelMarginOrder(BinanceSymbol.Name, orderId).Success;
        }

        public async Task<bool> CancelOrderAsync(long orderId)
        {
            return (await Client.Margin.Order.CancelMarginOrderAsync(BinanceSymbol.Name, orderId)).Success;
        }

        public void StartSocketConnections(Action<EventOrderBook> eventOrderBook, Action<Order> orderUpdate)
        {
            SubscribeToOrderBookUpdates(eventOrderBook);
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
            CallResult<UpdateSubscription> successAccount = SocketClient.Spot.SubscribeToUserDataUpdates(listenKey,
            null,// Handle account info data
            (BinanceStreamOrderUpdate data) => orderUpdate(new Order(data)), // Handle order update info data
            null, // Handler for OCO updates
            null, // Handler for position updates
            null); // Handler for account balance updates (withdrawals/deposits)

            UpdateSubscriptionAutoConnetionIfConnLost(successAccount, () => SubscribeToUserDataUpdates(orderUpdate));
        }

        private void SubscribeToOrderBookUpdates(Action<EventOrderBook> eventOrderBook)
        {
            CallResult<UpdateSubscription> successDepth = BinanceSocketClientBase.SubscribeToOrderBookUpdates(BinanceSymbol.Name, 1000, (IBinanceOrderBook data) =>
            {
                if (data.Asks.Any() && data.Bids.Any())
                {
                    eventOrderBook(new EventOrderBook(data.Asks.First().Price, data.Bids.First().Price));
                }
            });

            UpdateSubscriptionAutoConnetionIfConnLost(successDepth, () => SubscribeToOrderBookUpdates(eventOrderBook));
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

                Log.Information("Check - KeepAliveListenKey");
                if (!UserStream.KeepAliveUserStream(listenKey).Success)
                {
                    Log.Information("KeepAliveListenKey Fail execute GetListenKey");
                    GetListenKey();
                }
                else
                {
                    Log.Information("KeepAliveListenKey Success");
                }
            }
        }

        public void SubscribeToKlineUpdates(IList<IOhlcv> candlestick, KlineInterval interval, Action<IList<IOhlcv>, Candlestick> calculateIndicators)
        {
            CallResult<UpdateSubscription> successKline = SocketClient.Spot.SubscribeToKlineUpdates(BinanceSymbol.Name, interval, (IBinanceStreamKlineData data) =>
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
                   // SocketClient.Unsubscribe(updateSubscription.Data);
                   // callback();
                    Log.Fatal("ConnectionLost {0}", updateSubscription.Error?.Message);
                };

                updateSubscription.Data.Exception += (ex) =>
                {
                    Log.Fatal(ex, "ConnectionLost error unexpectedly.");
                };
            }
            else
            {
                Log.Fatal("UpdateSubscriptionAutoConnetionIfConnLost {0}", updateSubscription.Error?.Message);
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
            WebCallResult<BinanceMarginAccount> binanceMarginAccount = Client.Margin.GetMarginAccountInfo();
            if (binanceMarginAccount.Success)
            {
                var currentTradingAsset = binanceMarginAccount.Data.Balances.FirstOrDefault(x => x.Asset.ToLower() == asset.ToLower());
                return currentTradingAsset.Free;
            }
            else
            {
                Log.Fatal("{0}", binanceMarginAccount.Error?.Message);
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
