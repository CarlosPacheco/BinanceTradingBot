﻿using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.Clients;
using Binance.Net.Interfaces.Clients.SpotApi;
using Binance.Net.Objects.Models.Spot;
using Binance.Net.Objects.Models.Spot.Socket;
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
    public class BinanceClientSpot : BinanceClientBase, IBinanceClient
    {
        /// <summary>
        /// Spot streams and requests
        /// </summary>
        public IBinanceSocketClientSpotApi SocketApi { get; }

        /// <summary>
        /// Endpoints related to retrieving market and system data
        /// </summary>
        public IBinanceRestClientSpotApiExchangeData ExchangeData { get; private set; }

        /// <summary>
        /// Endpoints related to account settings, info or actions
        /// </summary>
        public IBinanceRestClientSpotApiAccount Account { get; private set; }

        /// <summary>
        /// Endpoints related to orders and trades
        /// </summary>
        public IBinanceRestClientSpotApiTrading Trading { get; private set; }

        public BinanceClientSpot(IBinanceSocketClient socketClient, IBinanceRestClient client, ILogger<BinanceClientSpot> logger) : base(socketClient, client, logger)
        {
            SocketApi = SocketClient.SpotApi;
            ExchangeData = Client.SpotApi.ExchangeData;
            Account = Client.SpotApi.Account;
            Trading = Client.SpotApi.Trading;

        }

        public void StartSocketConnections(string symbol, Action<EventOrderBook> eventOrderBook, Action<Order> orderUpdate)
        {
            BinanceSymbol binanceSymbol = ExchangeData.GetExchangeInfoAsync().Result.Data.Symbols.FirstOrDefault(x => x.Name == symbol);
            if (binanceSymbol == null) throw new Exception("Symbol dont exist!");
            BinanceSymbol = new AccountBinanceSymbol(binanceSymbol);
            Logger.LogInformation("BinanceSymbol: {0}", BinanceSymbol.Name);

            SubscribeToOrderBookUpdates(eventOrderBook);
            GetListenKey(Account.StartUserStreamAsync());
            SubscribeToUserDataUpdates(orderUpdate);
            KeepAliveListenKey(Account.KeepAliveUserStreamAsync(listenKey), Account.StartUserStreamAsync());
        }

        public bool TryPlaceOrder(OrderSide side, OrderType orderType, decimal quantity, decimal price, TimeInForce timeInForce, out Order order)
        {
            order = null;
            WebCallResult<BinancePlacedOrder> orderRequest = Trading.PlaceOrderAsync(BinanceSymbol.Name, side, GetOrderType(orderType), quantity, null, null, price, timeInForce).Result;
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
            WebCallResult<BinancePlacedOrder> orderRequest = await Trading.PlaceOrderAsync(BinanceSymbol.Name, side, GetOrderType(orderType), quantity, null, null, price, timeInForce);
            if (!orderRequest.Success)
            {
                Logger.LogInformation("error place {0} at: {1}", side, price);
                Logger.LogCritical("{0}", orderRequest.Error?.Message);
                return (false, null);
            }

            return (true, new Order(orderRequest.Data));
        }

        public bool TryGetOrder(long orderId, out Order order)
        {
            order = null;
            WebCallResult<BinanceOrder> orderRequest = Trading.GetOrderAsync(BinanceSymbol.Name, orderId).Result;
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
            return Trading.CancelOrderAsync(BinanceSymbol.Name, orderId).Result.Success;
        }

        public async Task<bool> CancelOrderAsync(long orderId)
        {
            return (await Trading.CancelOrderAsync(BinanceSymbol.Name, orderId)).Success;
        }


        private async void SubscribeToUserDataUpdates(Action<Order> orderUpdate)
        {
            if (string.IsNullOrWhiteSpace(listenKey))
            {
                Logger.LogCritical("ListenKey can't be null, maybe you have Api key Restrict access to trusted IPs only enabled");
                GetListenKey(Account.StartUserStreamAsync());
            }

            CallResult<UpdateSubscription> successAccount = await SocketApi.Account.SubscribeToUserDataUpdatesAsync(listenKey,
            (DataEvent<BinanceStreamOrderUpdate> dataEv) => orderUpdate(new Order(dataEv.Data)), // Handle order update info data
            null, // Handler for OCO updates
            null, // Handler for position updates
            null); // Handler for account balance updates (withdrawals/deposits)

            if (!successAccount.Success)
                Logger.LogCritical("SubscribeToUserDataUpdates {0}", successAccount.Error?.Message);
        }

        private async void SubscribeToOrderBookUpdates(Action<EventOrderBook> eventOrderBook)
        {
            CallResult<UpdateSubscription> successDepth = await SocketApi.ExchangeData.SubscribeToOrderBookUpdatesAsync(BinanceSymbol.Name, 1000, (DataEvent<IBinanceEventOrderBook> dataEv) =>
            {
                if (dataEv.Data.Asks.Any() && dataEv.Data.Bids.Any())
                {
                    eventOrderBook(new EventOrderBook(dataEv.Data.Asks.First().Price, dataEv.Data.Bids.First().Price));
                }
            });

            if (!successDepth.Success)
                Logger.LogCritical("SubscribeToOrderBookUpdates {0}", successDepth.Error?.Message);
        }

        public decimal GetFreeBaseBalance()
        {
            return GetFreeBalance(BinanceSymbol.BaseAsset);
        }

        public decimal GetFreeQuoteBalance()
        {
            return GetFreeBalance(BinanceSymbol.QuoteAsset);
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

        public long Ping()
        {
            return ExchangeData.PingAsync().Result.Data;
        }

        public decimal GetFreeBalance(string asset)
        {
            WebCallResult<BinanceAccountInfo> binanceMarginAccount = Account.GetAccountInfoAsync().Result;
            if (binanceMarginAccount.Success)
            {
                var currentTradingAsset = binanceMarginAccount.Data.Balances.FirstOrDefault(x => x.Asset.ToLower() == asset.ToLower());
                return currentTradingAsset.Available;
            }
            else
            {
                Logger.LogCritical("{0}", binanceMarginAccount.Error?.Message);
            }

            return 0;
        }

        private SpotOrderType GetOrderType(OrderType orderType)
        {
            switch (orderType)
            {
                case OrderType.Limit:
                    return SpotOrderType.Limit;                   
                case OrderType.Market:
                    return SpotOrderType.Market;
                case OrderType.StopLoss:
                    return SpotOrderType.StopLoss;
                case OrderType.StopLossLimit:
                    return SpotOrderType.StopLossLimit;
                case OrderType.TakeProfit:
                    return SpotOrderType.TakeProfit;
                case OrderType.TakeProfitLimit:
                    return SpotOrderType.TakeProfitLimit;
            }

            return SpotOrderType.Limit;
        }

        public void StopListenKey()
        {
            StopListenKey(Account.StopUserStreamAsync(listenKey));
        }
    }
}
