using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Futures.FuturesData;
using Binance.Net.Objects.Futures.MarketData;
using Binance.Net.Objects.Futures.MarketStream;
using Binance.Net.Objects.Futures.UserStream;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ImMillionaire.Brain.Core
{
    public class BinanceClientFutures : BinanceClientBase, IBinanceClient
    {
        public BinanceClientFutures(IBinanceSocketClient socketClient, Binance.Net.Interfaces.IBinanceClient client, ILogger logger) : base(socketClient, client, logger)
        {
            Market = Client.FuturesUsdt.Market;
            UserStream = Client.FuturesUsdt.UserStream;
            BinanceSocketClientBase = SocketClient.FuturesUsdt;
        }

        public void StartSocketConnections(string symbol, Action<EventOrderBook> eventOrderBook, Action<Order> orderUpdate)
        {
            BinanceFuturesSymbol binanceSymbol = Client.FuturesUsdt.System.GetExchangeInfo().Data.Symbols.FirstOrDefault(x => x.Name == symbol);
            if (binanceSymbol == null) throw new Exception("Symbol don't exist!");
            BinanceSymbol = new AccountBinanceSymbol(binanceSymbol);
            CalculateDecimalAmount(BinanceSymbol.LotSizeFilter.StepSize);
            Logger.Information("BinanceSymbol: {0}", BinanceSymbol.Name);

            SubscribeToBookTickerUpdates(eventOrderBook);
            GetListenKey();
            SubscribeToUserDataUpdates(orderUpdate);
            KeepAliveListenKey();
        }

        public bool TryPlaceOrder(OrderSide side, OrderType type, decimal quantity, decimal price, TimeInForce timeInForce, out Order order)
        {
            order = null;
            WebCallResult<BinanceFuturesPlacedOrder> orderRequest = Client.FuturesUsdt.Order.PlaceOrder(BinanceSymbol.Name, side, type, quantity, PositionSide.Both, timeInForce, null, price);
            if (!orderRequest.Success)
            {
                Logger.Fatal("{0}", orderRequest.Error?.Message);
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
                Logger.Fatal("{0}", orderRequest.Error?.Message);
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
                Logger.Warning($"error place {side} at: {price}");
                Logger.Fatal("{0}", orderRequest.Error?.Message);
                return (false, null);
            }

            return (true, new Order(orderRequest.Data));
        }

        private void SubscribeToUserDataUpdates(Action<Order> orderUpdate)
        {
            if (string.IsNullOrWhiteSpace(listenKey))
            {
                Logger.Fatal("ListenKey can't be null, maybe you have Api key Restrict access to trusted IPs only enabled");
                GetListenKey();
            }
            CallResult<UpdateSubscription> successAccount = SocketClient.FuturesUsdt.SubscribeToUserDataUpdates(listenKey,
            null, // onMarginUpdate
            null, // onAccountUpdate      
            (BinanceFuturesStreamOrderUpdate data) => orderUpdate(new Order(data.UpdateData)), // onOrderUpdate
            null); // onListenKeyExpired

            if (!successAccount.Success)
                Logger.Fatal("SubscribeToUserDataUpdates {0}", successAccount.Error?.Message);
        }

        private void SubscribeToBookTickerUpdates(Action<EventOrderBook> eventOrderBook)
        {
            CallResult<UpdateSubscription> success = SocketClient.FuturesUsdt.SubscribeToBookTickerUpdates(BinanceSymbol.Name, (BinanceFuturesStreamBookPrice data) => eventOrderBook(new EventOrderBook(data.BestAskPrice, data.BestBidPrice)));
            if (!success.Success)
                Logger.Fatal("SubscribeToBookTickerUpdates {0}", success.Error?.Message);
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
                Logger.Fatal("{0}", binanceAccount.Error?.Message);
            }

            return 0;
        }
    }
}
