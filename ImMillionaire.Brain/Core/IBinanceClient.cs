using Binance.Net.Enums;
using Binance.Net.Interfaces.SocketSubClient;
using Binance.Net.Interfaces.SubClients;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain.Core
{
    public interface IBinanceClient : IDisposable
    {
        long Ping();

        decimal GetCurrentTradePrice { get; }

        AccountBinanceSymbol GetAccountBinanceSymbol();

        int DecimalQuantity { get; }

        int DecimalPrice { get; }

        IBinanceClientMarket Market { get; }

        IBinanceClientUserStream UserStream { get; }

        IBinanceSocketClientBase BinanceSocketClientBase { get; }

        IList<IOhlcv> GetKlines(KlineInterval klineInterval);

        bool TryPlaceOrder(OrderSide side, OrderType type, decimal quantity, decimal price, TimeInForce timeInForce, out Order order);

        Task<(bool IsSucess, Order Order)> TryPlaceOrderAsync(OrderSide side, OrderType type, decimal quantity, decimal price, TimeInForce timeInForce);

        bool TryGetOrder(long orderId, out Order order);

        bool CancelOrder(long orderId);

        Task<bool> CancelOrderAsync(long orderId);

        decimal GetFreeBaseBalance();

        decimal GetFreeQuoteBalance();

        decimal GetFreeBalance(string asset);

        void SubscribeToKlineUpdates(IList<IOhlcv> candlestick, KlineInterval interval, Action<IList<IOhlcv>, Candlestick> calculateIndicators);

        void StartSocketConnections(string symbol, Action<EventOrderBook> eventOrderBook, Action<Order> orderUpdate);
    }
}
