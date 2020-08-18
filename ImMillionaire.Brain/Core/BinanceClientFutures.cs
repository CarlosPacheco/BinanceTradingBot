using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot;
using Binance.Net.Objects.Spot.MarketData;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain.Core
{
    public class BinanceClientFutures : IBinanceClient
    {
        private IBinanceFuturesSocketClient SocketClient { get; set; }

        private IBinanceFuturesClient Client { get; set; }

        public ConfigOptions Configuration { get; }

        private string Symbol { get; set; }

        public decimal GetCurrentTradePrice { get; set; }

        public BinanceClientFutures(ConfigOptions config)
        {
            Configuration = config;
            Symbol = config.Symbol;

            BinanceClient.SetDefaultOptions(new BinanceClientOptions()
            {
                ApiCredentials = new ApiCredentials(config.ApiKey, config.SecretKey)
            });
            BinanceSocketClient.SetDefaultOptions(new BinanceSocketClientOptions()
            {
                ApiCredentials = new ApiCredentials(config.ApiKey, config.SecretKey)
            });

            SocketClient = new BinanceFuturesSocketClient();
            Client = new BinanceFuturesClient();
        }

        public IList<IOhlcv> GetKlines(KlineInterval klineInterval)
        {
            WebCallResult<IEnumerable<BinanceKline>> klines = Client.GetKlines(Symbol, klineInterval);
            if (klines.Success)
            {
                return klines.Data.Select(k => new Candlestick(k)).ToList<IOhlcv>();
            }

            return null;
        }

        public long Ping()
        {
            return Client.Ping().Data;
        }

        public AccountBinanceSymbol GetAccountBinanceSymbol()
        {
            return new AccountBinanceSymbol(Client.GetExchangeInfo().Data.Symbols.FirstOrDefault(x => x.Name == Symbol));
        }

        public bool TryPlaceOrder(OrderSide side, OrderType type, decimal quantity, decimal price, TimeInForce timeInForce, out Order order)
        {
            order = null;
            var orderRequest = Client.PlaceOrder(Symbol, side, type, quantity, PositionSide.Both, timeInForce, null, price);
            if (!orderRequest.Success) return false;

            order = new Order(orderRequest.Data);
            return true;
        }

        public bool TryGetOrder(long orderId, out Order order)
        {
            order = null;

            var orderRequest = Client.GetOrder(Symbol, orderId);
            if (!orderRequest.Success) return false;

            order = new Order(orderRequest.Data);
            return true;
        }

        public bool CancelOrder(long orderId)
        {
            return Client.CancelOrder(Symbol, orderId).Success;
        }

        public async Task<bool> CancelOrderAsync(long orderId)
        {
            return (await Client.CancelOrderAsync(Symbol, orderId)).Success;
        }

        public void Dispose()
        {
            Client?.Dispose();
            SocketClient?.UnsubscribeAll();
            SocketClient?.Dispose();
        }

        public decimal GetFreeBalance()
        {
            throw new NotImplementedException();
        }

        public Task<(bool IsSucess, Order Order)> TryPlaceOrderAsync(OrderSide side, OrderType type, decimal quantity, decimal price, TimeInForce timeInForce)
        {
            throw new NotImplementedException();
        }

        public void SubscribeToKlineUpdates(IList<IOhlcv> candlestick, KlineInterval interval, Action<IList<IOhlcv>, Candlestick> calculateIndicators)
        {
            throw new NotImplementedException();
        }

        public void StartSocketConnections(Action<EventOrderBook> eventOrderBook, Action<Order> orderUpdate)
        {
            throw new NotImplementedException();
        }
    }
}
