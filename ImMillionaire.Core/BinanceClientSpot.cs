﻿using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.SocketSubClient;
using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.Objects.Spot.SpotData;
using Binance.Net.Objects.Spot.UserStream;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ImMillionaire.Core
{
    public class BinanceClientSpot : BinanceClientBase, IBinanceClient
    {
        public IBinanceSocketClientSpot BinanceSocketClientSpot { get; }

        public BinanceClientSpot(IBinanceSocketClient socketClient, Binance.Net.Interfaces.IBinanceClient client, ILogger<BinanceClientSpot> logger) : base(socketClient, client, logger)
        {
            Market = Client.Spot.Market;
            UserStream = Client.Spot.UserStream;
            BinanceSocketClientBase = BinanceSocketClientSpot = SocketClient.Spot;
        }

        public void StartSocketConnections(string symbol, Action<EventOrderBook> eventOrderBook, Action<Order> orderUpdate)
        {
            BinanceSymbol binanceSymbol = Client.Spot.System.GetExchangeInfoAsync().Result.Data.Symbols.FirstOrDefault(x => x.Name == symbol);
            if (binanceSymbol == null) throw new Exception("Symbol dont exist!");
            BinanceSymbol = new AccountBinanceSymbol(binanceSymbol);
            Logger.LogInformation("BinanceSymbol: {0}", BinanceSymbol.Name);

            SubscribeToOrderBookUpdates(eventOrderBook);
            GetListenKey();
            SubscribeToUserDataUpdates(orderUpdate);
            KeepAliveListenKey();
        }

        public bool TryPlaceOrder(OrderSide side, OrderType type, decimal quantity, decimal price, TimeInForce timeInForce, out Order order)
        {
            order = null;
            WebCallResult<BinancePlacedOrder> orderRequest = Client.Spot.Order.PlaceOrderAsync(BinanceSymbol.Name, side, type, quantity, null, null, price, timeInForce).Result;
            if (!orderRequest.Success)
            {
                Logger.LogCritical("{0}", orderRequest.Error?.Message);
                return false;
            }

            order = new Order(orderRequest.Data);
            return true;
        }

        public async Task<(bool IsSucess, Order Order)> TryPlaceOrderAsync(OrderSide side, OrderType type, decimal quantity, decimal price, TimeInForce timeInForce)
        {
            WebCallResult<BinancePlacedOrder> orderRequest = await Client.Spot.Order.PlaceOrderAsync(BinanceSymbol.Name, side, type, quantity, null, null, price, timeInForce);
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
            WebCallResult<BinanceOrder> orderRequest = Client.Spot.Order.GetOrderAsync(BinanceSymbol.Name, orderId).Result;
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
            return Client.Spot.Order.CancelOrderAsync(BinanceSymbol.Name, orderId).Result.Success;
        }

        public async Task<bool> CancelOrderAsync(long orderId)
        {
            return (await Client.Spot.Order.CancelOrderAsync(BinanceSymbol.Name, orderId)).Success;
        }

        private async void SubscribeToUserDataUpdates(Action<Order> orderUpdate)
        {
            if (string.IsNullOrWhiteSpace(listenKey))
            {
                Logger.LogCritical("ListenKey can't be null, maybe you have Api key Restrict access to trusted IPs only enabled");
                GetListenKey();
            }

            CallResult<UpdateSubscription> successAccount = await BinanceSocketClientSpot.SubscribeToUserDataUpdatesAsync(listenKey,
            (DataEvent<BinanceStreamOrderUpdate> dataEv) => orderUpdate(new Order(dataEv.Data)), // Handle order update info data
            null, // Handler for OCO updates
            null, // Handler for position updates
            null); // Handler for account balance updates (withdrawals/deposits)

            if (!successAccount.Success)
                Logger.LogCritical("SubscribeToUserDataUpdates {0}", successAccount.Error?.Message);
        }

        private async void SubscribeToOrderBookUpdates(Action<EventOrderBook> eventOrderBook)
        {
            CallResult<UpdateSubscription> successDepth = await BinanceSocketClientSpot.SubscribeToOrderBookUpdatesAsync(BinanceSymbol.Name, 1000, (DataEvent<IBinanceEventOrderBook> dataEv) =>
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

        public decimal GetFreeBalance(string asset)
        {
            WebCallResult<BinanceAccountInfo> binanceMarginAccount = Client.General.GetAccountInfoAsync().Result;
            if (binanceMarginAccount.Success)
            {
                var currentTradingAsset = binanceMarginAccount.Data.Balances.FirstOrDefault(x => x.Asset.ToLower() == asset.ToLower());
                return currentTradingAsset.Free;
            }
            else
            {
                Logger.LogCritical("{0}", binanceMarginAccount.Error?.Message);
            }

            return 0;
        }
    }
}
