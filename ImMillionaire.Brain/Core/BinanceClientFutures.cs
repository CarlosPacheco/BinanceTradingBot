using Binance.Net.Enums;
using Binance.Net.Objects.Futures.FuturesData;
using Binance.Net.Objects.Futures.MarketData;
using Binance.Net.Objects.Futures.MarketStream;
using Binance.Net.Objects.Futures.UserStream;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ImMillionaire.Brain.Core
{
    public class BinanceClientFutures : BinanceClientBase, IBinanceClient
    {
        public BinanceClientFutures(IOptions<ConfigOptions> config): base(config)
        {
            Market = Client.FuturesUsdt.Market;
            UserStream = Client.FuturesUsdt.UserStream;
            BinanceSocketClientBase = SocketClient.FuturesUsdt;

            BinanceFuturesSymbol symbol = Client.FuturesUsdt.System.GetExchangeInfo().Data.Symbols.FirstOrDefault(x => x.Name == Configuration.Symbol);
            if (symbol == null) throw new Exception("Symbol don't exist!");
            BinanceSymbol = new AccountBinanceSymbol(symbol);
            CalculateDecimalAmount(symbol.LotSizeFilter.StepSize);
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
    }
}
