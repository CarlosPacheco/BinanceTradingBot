using Binance.Net.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Core
{
    public interface IBinanceClient : IDisposable
    {
        long Ping();

        decimal GetCurrentTradePrice { get; }

        AccountBinanceSymbol GetAccountBinanceSymbol();

        IList<IOhlcv> GetKlines(KlineInterval klineInterval);

        bool TryPlaceOrder(OrderSide side, OrderType orderType, decimal quantity, decimal price, TimeInForce timeInForce, out Order order);

        Task<(bool IsSucess, Order Order)> TryPlaceOrderAsync(OrderSide side, OrderType orderType, decimal quantity, decimal price, TimeInForce timeInForce);

        bool TryGetOrder(long orderId, out Order order);

        bool CancelOrder(long orderId);

        Task<bool> CancelOrderAsync(long orderId);

        decimal GetFreeBaseBalance();

        decimal GetFreeQuoteBalance();

        decimal GetFreeBalance(string asset);

        void SubscribeToKlineUpdates(IList<IOhlcv> candlestick, KlineInterval interval, Action<IList<IOhlcv>, Candlestick> calculateIndicators);

        void StartSocketConnections(string symbol, Action<EventOrderBook> eventOrderBook, Action<Order> orderUpdate);

        void StopListenKey();
    }
}
