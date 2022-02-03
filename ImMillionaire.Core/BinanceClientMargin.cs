using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.SocketSubClient;
using Binance.Net.Objects.Spot.MarginData;
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
    public class BinanceClientMargin : BinanceClientBase, IBinanceClient
    {
        public IBinanceSocketClientSpot BinanceSocketClientSpot { get; }

        public BinanceClientMargin(IBinanceSocketClient socketClient, Binance.Net.Interfaces.IBinanceClient client, ILogger<BinanceClientMargin> logger) : base(socketClient, client, logger)
        {
            Market = Client.Spot.Market;
            UserStream = Client.Margin.UserStream;
            BinanceSocketClientBase = BinanceSocketClientSpot = SocketClient.Spot;
        }

        public void StartSocketConnections(string symbol, Action<EventOrderBook> eventOrderBook, Action<Order> orderUpdate)
        {
            WebCallResult<BinanceExchangeInfo> exchangeInfo = Client.Spot.System.GetExchangeInfoAsync().Result;
            if (!exchangeInfo.Success)
            {
                Logger.LogCritical("error exchangeInfo  {0}", exchangeInfo.Error?.Message);
            }

            BinanceSymbol binanceSymbol = exchangeInfo.Data.Symbols.FirstOrDefault(x => x.Name == symbol);
            if (binanceSymbol == null) throw new Exception("Symbol don't exist!");
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
            WebCallResult<BinancePlacedOrder> orderRequest = Client.Margin.Order.PlaceMarginOrderAsync(BinanceSymbol.Name, side, type, quantity, null, null, price, timeInForce).Result;
            if (!orderRequest.Success)
            {
                Logger.LogInformation("error place {0} at: {1}", side, price);
                Logger.LogCritical("{0}", orderRequest.Error?.Message);
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
                Logger.LogInformation("error place {0} at: {1}", side, price);
                Logger.LogCritical("{0}", orderRequest.Error?.Message);
                return (false, null);
            }

            return (true, new Order(orderRequest.Data));
        }

        public bool TryGetOrder(long orderId, out Order order)
        {
            order = null;
            WebCallResult<BinanceOrder> orderRequest = Client.Margin.Order.GetMarginAccountOrderAsync(BinanceSymbol.Name, orderId).Result;
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
            return Client.Margin.Order.CancelMarginOrderAsync(BinanceSymbol.Name, orderId).Result.Success;
        }

        public async Task<bool> CancelOrderAsync(long orderId)
        {
            return (await Client.Margin.Order.CancelMarginOrderAsync(BinanceSymbol.Name, orderId)).Success;
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
            WebCallResult<BinanceMarginAccount> binanceMarginAccount = Client.Margin.GetMarginAccountInfoAsync().Result;
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
