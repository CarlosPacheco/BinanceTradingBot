using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.SocketSubClient;
using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.Objects.Spot.SpotData;
using Binance.Net.Objects.Spot.UserStream;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ImMillionaire.Brain.Core
{
    public class BinanceClientSpot : BinanceClientBase, IBinanceClient
    {
        public IBinanceSocketClientSpot BinanceSocketClientSpot { get; }

        public BinanceClientSpot(IBinanceSocketClient socketClient, Binance.Net.Interfaces.IBinanceClient client, ILogger logger) : base(socketClient, client, logger)
        {
            Market = Client.Spot.Market;
            UserStream = Client.Spot.UserStream;
            BinanceSocketClientBase = BinanceSocketClientSpot = SocketClient.Spot;
        }

        public void StartSocketConnections(string symbol, Action<EventOrderBook> eventOrderBook, Action<Order> orderUpdate)
        {
            BinanceSymbol binanceSymbol = Client.Spot.System.GetExchangeInfo().Data.Symbols.FirstOrDefault(x => x.Name == symbol);
            if (binanceSymbol == null) throw new Exception("Symbol dont exist!");
            BinanceSymbol = new AccountBinanceSymbol(binanceSymbol);
            Logger.Information("BinanceSymbol: {0}", BinanceSymbol.Name);

            SubscribeToOrderBookUpdates(eventOrderBook);
            GetListenKey();
            SubscribeToUserDataUpdates(orderUpdate);
            KeepAliveListenKey();
        }

        public bool TryPlaceOrder(OrderSide side, OrderType type, decimal quantity, decimal price, TimeInForce timeInForce, out Order order)
        {
            order = null;
            WebCallResult<BinancePlacedOrder> orderRequest = Client.Spot.Order.PlaceOrder(BinanceSymbol.Name, side, type, quantity, null, null, price, timeInForce);
            if (!orderRequest.Success)
            {
                Logger.Fatal("{0}", orderRequest.Error?.Message);
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
                Logger.Information("error place {0} at: {1}", side, price);
                Logger.Fatal("{0}", orderRequest.Error?.Message);
                return (false, null);
            }

            return (true, new Order(orderRequest.Data));
        }

        public bool TryGetOrder(long orderId, out Order order)
        {
            order = null;
            WebCallResult<BinanceOrder> orderRequest = Client.Spot.Order.GetOrder(BinanceSymbol.Name, orderId);
            if (!orderRequest.Success)
            {
                Logger.Fatal("{0}", orderRequest.Error?.Message);
                return false;
            }

            order = new Order(orderRequest.Data);
            return true;
        }

        public bool CancelOrder(long orderId)
        {
            return Client.Spot.Order.CancelOrder(BinanceSymbol.Name, orderId).Success;
        }

        public async Task<bool> CancelOrderAsync(long orderId)
        {
            return (await Client.Spot.Order.CancelOrderAsync(BinanceSymbol.Name, orderId)).Success;
        }

        private void SubscribeToUserDataUpdates(Action<Order> orderUpdate)
        {
            if (string.IsNullOrWhiteSpace(listenKey))
            {
                Logger.Fatal("ListenKey can't be null, maybe you have Api key Restrict access to trusted IPs only enabled");
                GetListenKey();
            }

            CallResult<UpdateSubscription> successAccount = BinanceSocketClientSpot.SubscribeToUserDataUpdates(listenKey,
            null,// Handle account info data
            (BinanceStreamOrderUpdate data) => orderUpdate(new Order(data)), // Handle order update info data
            null, // Handler for OCO updates
            null, // Handler for position updates
            null); // Handler for account balance updates (withdrawals/deposits)

            if (!successAccount.Success)
                Logger.Fatal("SubscribeToUserDataUpdates {0}", successAccount.Error?.Message);
        }

        private void SubscribeToOrderBookUpdates(Action<EventOrderBook> eventOrderBook)
        {
            CallResult<UpdateSubscription> successDepth = BinanceSocketClientSpot.SubscribeToOrderBookUpdates(BinanceSymbol.Name, 1000, (IBinanceEventOrderBook data) =>
            {
                if (data.Asks.Any() && data.Bids.Any())
                {
                    eventOrderBook(new EventOrderBook(data.Asks.First().Price, data.Bids.First().Price));
                }
            });

            if (!successDepth.Success)
                Logger.Fatal("SubscribeToOrderBookUpdates {0}", successDepth.Error?.Message);
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
            WebCallResult<BinanceAccountInfo> binanceMarginAccount = Client.General.GetAccountInfo();
            if (binanceMarginAccount.Success)
            {
                var currentTradingAsset = binanceMarginAccount.Data.Balances.FirstOrDefault(x => x.Asset.ToLower() == asset.ToLower());
                return currentTradingAsset.Free;
            }
            else
            {
                Logger.Fatal("{0}", binanceMarginAccount.Error?.Message);
            }

            return 0;
        }
    }
}
