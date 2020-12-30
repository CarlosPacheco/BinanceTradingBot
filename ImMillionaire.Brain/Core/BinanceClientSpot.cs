using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.Objects.Spot.SpotData;
using Binance.Net.Objects.Spot.UserStream;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ImMillionaire.Brain.Core
{
    public class BinanceClientSpot : BinanceClientBase, IBinanceClient
    {
        public BinanceClientSpot(IOptions<ConfigOptions> config) : base(config)
        {
            Market = Client.Spot.Market;
            UserStream = Client.Spot.UserStream;
            BinanceSocketClientBase = SocketClient.Spot;

            BinanceSymbol symbol = Client.Spot.System.GetExchangeInfo().Data.Symbols.FirstOrDefault(x => x.Name == Configuration.Symbol);
            if (symbol == null) throw new Exception("Symbol dont exist!");
            BinanceSymbol = new AccountBinanceSymbol(symbol);
            CalculateDecimalAmount(symbol.LotSizeFilter.StepSize);
        }

        public bool TryPlaceOrder(OrderSide side, OrderType type, decimal quantity, decimal price, TimeInForce timeInForce, out Order order)
        {
            order = null;
            WebCallResult<BinancePlacedOrder> orderRequest = Client.Spot.Order.PlaceOrder(BinanceSymbol.Name, side, type, quantity, null, null, price, timeInForce);
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
            WebCallResult<BinanceOrder> orderRequest = Client.Spot.Order.GetOrder(BinanceSymbol.Name, orderId);
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
            return Client.Spot.Order.CancelOrder(BinanceSymbol.Name, orderId).Success;
        }

        public async Task<bool> CancelOrderAsync(long orderId)
        {
            return (await Client.Spot.Order.CancelOrderAsync(BinanceSymbol.Name, orderId)).Success;
        }

        public async Task<(bool IsSucess, Order Order)> TryPlaceOrderAsync(OrderSide side, OrderType type, decimal quantity, decimal price, TimeInForce timeInForce)
        {
            WebCallResult<BinancePlacedOrder> orderRequest = await Client.Spot.Order.PlaceOrderAsync(BinanceSymbol.Name, side, type, quantity, null, null, price, timeInForce);
            if (!orderRequest.Success)
            {
                Log.Information("error place {0} at: {1}", side, price);
                Log.Fatal("{0}", orderRequest.Error?.Message);
                return (false, null);
            }

            return (true, new Order(orderRequest.Data));
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
            CallResult<UpdateSubscription> successDepth = SocketClient.Spot.SubscribeToOrderBookUpdates(BinanceSymbol.Name, 1000, (IBinanceOrderBook data) =>
            {
                if (data.Asks.Any() && data.Bids.Any())
                {
                    eventOrderBook(new EventOrderBook(data.Asks.First().Price, data.Bids.First().Price));
                }
            });

            UpdateSubscriptionAutoConnetionIfConnLost(successDepth, () => SubscribeToOrderBookUpdates(eventOrderBook));
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
                Log.Fatal("{0}", binanceMarginAccount.Error?.Message);
            }

            return 0;
        }
    }
}
