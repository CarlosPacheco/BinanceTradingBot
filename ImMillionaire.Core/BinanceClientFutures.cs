using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.Clients;
using Binance.Net.Interfaces.Clients.UsdFuturesApi;
using Binance.Net.Objects.Models.Futures;
using Binance.Net.Objects.Models.Futures.Socket;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Core
{
    public class BinanceClientFutures : BinanceClientBase, IBinanceClient
    {
        /// <summary>
        /// Usd futures streams
        /// </summary>
        public IBinanceSocketClientUsdFuturesApi SocketApi { get; private set; }

        /// <summary>
        /// Endpoints related to retrieving market data
        /// </summary>
        public IBinanceRestClientUsdFuturesApiExchangeData ExchangeData { get; private set; }

        /// <summary>
        /// Endpoints related to account settings, info or actions
        /// </summary>
        public IBinanceRestClientUsdFuturesApiAccount Account { get; private set; }

        /// <summary>
        /// Endpoints related to orders and trades
        /// </summary>
        public IBinanceRestClientUsdFuturesApiTrading Trading { get; private set; }

        public BinanceClientFutures(IBinanceSocketClient socketClient, IBinanceRestClient client, ILogger<BinanceClientFutures> logger) : base(socketClient, client, logger)
        {
            SocketApi = SocketClient.UsdFuturesApi;
            ExchangeData = Client.UsdFuturesApi.ExchangeData;
            Account = Client.UsdFuturesApi.Account;
            Trading = Client.UsdFuturesApi.Trading; 
        }

        public void StartSocketConnections(string symbol, Action<EventOrderBook> eventOrderBook, Action<Order> orderUpdate)
        {
            BinanceFuturesSymbol binanceSymbol = ExchangeData.GetExchangeInfoAsync().Result.Data.Symbols.FirstOrDefault(x => x.Name == symbol);
            if (binanceSymbol == null) throw new Exception("Symbol don't exist!");
            BinanceSymbol = new AccountBinanceSymbol(binanceSymbol);
            Logger.LogInformation("BinanceSymbol: {0}", BinanceSymbol.Name);

            SubscribeToBookTickerUpdates(eventOrderBook);
            GetListenKey(Account.StartUserStreamAsync());
            SubscribeToUserDataUpdates(orderUpdate);
            KeepAliveListenKey(Account.KeepAliveUserStreamAsync(listenKey), Account.StartUserStreamAsync());
        }

        public bool TryPlaceOrder(OrderSide side, OrderType orderType, decimal quantity, decimal price, TimeInForce timeInForce, out Order order)
        {
            order = null;
            WebCallResult<BinanceUsdFuturesOrder> orderRequest = Trading.PlaceOrderAsync(BinanceSymbol.Name, side, GetOrderType(orderType), quantity, price, PositionSide.Both, timeInForce).Result;
            if (!orderRequest.Success)
            {
                Logger.LogCritical("{0}", orderRequest.Error?.Message);
                return false;
            }

            order = new Order(orderRequest.Data);
            return true;
        }

        public async Task<(bool IsSucess, Order Order)> TryPlaceOrderAsync(OrderSide side, OrderType orderType, decimal quantity, decimal price, TimeInForce timeInForce)
        {
            WebCallResult<BinanceUsdFuturesOrder> orderRequest = await Trading.PlaceOrderAsync(BinanceSymbol.Name, side, GetOrderType(orderType), quantity, price, PositionSide.Both, timeInForce);
            if (!orderRequest.Success)
            {
                Logger.LogWarning($"error place {side} at: {price}");
                Logger.LogCritical("{0}", orderRequest.Error?.Message);
                return (false, null);
            }

            return (true, new Order(orderRequest.Data));
        }

        public bool TryGetOrder(long orderId, out Order order)
        {
            order = null;
            WebCallResult<BinanceUsdFuturesOrder> orderRequest = Trading.GetOrderAsync(BinanceSymbol.Name, orderId).Result;
            if (!orderRequest.Success)
            {
                Logger.LogCritical("{0}", orderRequest.Error?.Message);
                return false;
            }

            order = new Order(orderRequest.Data);
            return true;
        }

        public bool CancelOrder(long orderId)
        {
            return Client.UsdFuturesApi.Trading.CancelOrderAsync(BinanceSymbol.Name, orderId).Result.Success;
        }

        public async Task<bool> CancelOrderAsync(long orderId)
        {
            return (await Client.UsdFuturesApi.Trading.CancelOrderAsync(BinanceSymbol.Name, orderId)).Success;
        }

        private async void SubscribeToUserDataUpdates(Action<Order> orderUpdate)
        {
            if (string.IsNullOrWhiteSpace(listenKey))
            {
                Logger.LogCritical("ListenKey can't be null, maybe you have Api key Restrict access to trusted IPs only enabled");
                GetListenKey(Account.StartUserStreamAsync());
            }
            CallResult<UpdateSubscription> successAccount = await SocketApi.Account.SubscribeToUserDataUpdatesAsync(listenKey,
            null, // onLeverageUpdate
            null, // onMarginUpdate
            null, // onAccountUpdate      
            (DataEvent<BinanceFuturesStreamOrderUpdate> dataEv) => orderUpdate(new Order(dataEv.Data.UpdateData)), // onOrderUpdate
            null, // onListenKeyExpired
            null, // onStrategyUpdate
            null, // onGridUpdate
            null // onConditionalOrderTriggerRejectUpdate
            );

            if (!successAccount.Success)
                Logger.LogCritical("SubscribeToUserDataUpdates {0}", successAccount.Error?.Message);
        }

        private async void SubscribeToBookTickerUpdates(Action<EventOrderBook> eventOrderBook)
        {
            CallResult<UpdateSubscription> success = await SocketApi.ExchangeData.SubscribeToBookTickerUpdatesAsync(BinanceSymbol.Name, (DataEvent<BinanceFuturesStreamBookPrice> dataEv) => eventOrderBook(new EventOrderBook(dataEv.Data.BestAskPrice, dataEv.Data.BestBidPrice)));
            if (!success.Success)
                Logger.LogCritical("SubscribeToBookTickerUpdates {0}", success.Error?.Message);
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
            WebCallResult<BinanceFuturesAccountInfoV3> binanceAccount = Account.GetAccountInfoV3Async().Result;
            if (binanceAccount.Success)
            {
                //var currentTradingAsset = binanceAccount.Data.Assets.FirstOrDefault(x => x.Asset.ToLower() == asset.ToLower());
                //var currentTradingPosition = binanceAccount.Data.Positions.FirstOrDefault(x => x.Symbol == BinanceSymbol.Name);
                //return binanceAccount.Data.MaxWithdrawQuantity * currentTradingPosition.Leverage;
                return binanceAccount.Data.AvailableBalance;
            }
            else
            {
                Logger.LogCritical("{0}", binanceAccount.Error?.Message);
            }

            return 0;
        }

        private FuturesOrderType GetOrderType(OrderType orderType)
        {
            switch (orderType)
            {
                case OrderType.Limit:
                    return FuturesOrderType.Limit;
                case OrderType.Market:
                    return FuturesOrderType.Market;
                case OrderType.StopLoss:
                    return FuturesOrderType.Stop;
                case OrderType.StopLossLimit:
                    return FuturesOrderType.StopMarket;
                case OrderType.TakeProfit:
                    return FuturesOrderType.TakeProfit;
                case OrderType.TakeProfitLimit:
                    return FuturesOrderType.TakeProfitMarket;
            }

            return FuturesOrderType.Limit;
        }

        public long Ping()
        {
            return ExchangeData.PingAsync().Result.Data;
        }

        public IList<IOhlcv> GetKlines(KlineInterval klineInterval)
        {
            WebCallResult<IEnumerable<IBinanceKline>> klines = ExchangeData.GetKlinesAsync(BinanceSymbol.Name, klineInterval).Result;
            if (klines.Success)
            {
                return klines.Data.Select(k => new Candlestick(k)).ToList<IOhlcv>();
            }
            else
            {
                Logger.LogCritical("{0}", klines.Error?.Message);
            }

            return null;
        }

        public async void SubscribeToKlineUpdates(IList<IOhlcv> candlestick, KlineInterval interval, Action<IList<IOhlcv>, Candlestick> calculateIndicators)
        {
            CallResult<UpdateSubscription> successKline = await SocketApi.ExchangeData.SubscribeToKlineUpdatesAsync(BinanceSymbol.Name, interval, (DataEvent<IBinanceStreamKlineData> dataEv) =>
            {
                Candlestick candle = new Candlestick(dataEv.Data.Data);
                candlestick.Add(candle);

                calculateIndicators(candlestick, candle);

                if (!dataEv.Data.Data.Final)
                {
                    candlestick.Remove(candle);
                }
            });

            if (!successKline.Success)
                Logger.LogCritical("SubscribeToKlineUpdates {0}", successKline.Error?.Message);
        }

        public void StopListenKey()
        {
            StopListenKey(Account.StopUserStreamAsync(listenKey));
        }

        protected void GetListenKey(Task<WebCallResult<string>> taskStartUser)
        {
            WebCallResult<string> result = taskStartUser.Result;
            if (result.Success)
            {
                listenKey = result.Data;
            }
            else
            {
                Logger.LogCritical("{0} - GetListenKey", result.Error?.Message);
            }
        }

        protected async void KeepAliveListenKey(Task<WebCallResult> taskKeepAlive, Task<WebCallResult<string>> taskStartUser)
        {
            while (!tokenSource.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(new TimeSpan(0, 30, 0), tokenSource.Token);

                    if (!taskKeepAlive.Result.Success)
                    {
                        Logger.LogInformation("KeepAliveListenKey Fail execute GetListenKey");
                        GetListenKey(taskStartUser);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        public void StopListenKey(Task<WebCallResult> taskStopUser)
        {
            if (!string.IsNullOrWhiteSpace(listenKey)) _ = taskStopUser;
        }
    }
}
