using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.SocketSubClient;
using Binance.Net.Interfaces.SubClients;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain.Core
{
    public abstract class BinanceClientBase : IDisposable
    {
        protected ILogger Logger { get; }

        protected IBinanceSocketClient SocketClient { get; }

        protected Binance.Net.Interfaces.IBinanceClient Client { get; }

        protected CancellationTokenSource tokenSource = new CancellationTokenSource();

        public IBinanceClientMarket Market { get; protected set; }

        public IBinanceClientUserStream UserStream { get; protected set; }

        public IBinanceSocketClientBase BinanceSocketClientBase { get; protected set; }

        protected AccountBinanceSymbol BinanceSymbol { get; set; }

        protected string listenKey;

        public decimal GetCurrentTradePrice { get; set; }

        public BinanceClientBase(IBinanceSocketClient socketClient, Binance.Net.Interfaces.IBinanceClient client, ILogger logger)
        {
            SocketClient = socketClient;
            Client = client;
            Logger = logger;
        }

        public IList<IOhlcv> GetKlines(KlineInterval klineInterval)
        {
            WebCallResult<IEnumerable<IBinanceKline>> klines = Market.GetKlines(BinanceSymbol.Name, klineInterval);
            if (klines.Success)
            {
                return klines.Data.Select(k => new Candlestick(k)).ToList<IOhlcv>();
            }
            else
            {
                Logger.Fatal("{0}", klines.Error?.Message);
            }

            return null;
        }

        public void SubscribeToKlineUpdates(IList<IOhlcv> candlestick, KlineInterval interval, Action<IList<IOhlcv>, Candlestick> calculateIndicators)
        {
            CallResult<UpdateSubscription> successKline = BinanceSocketClientBase.SubscribeToKlineUpdates(BinanceSymbol.Name, interval, (IBinanceStreamKlineData data) =>
            {
                Candlestick candle = new Candlestick(data.Data);
                candlestick.Add(candle);

                calculateIndicators(candlestick, candle);

                if (!data.Data.Final)
                {
                    candlestick.Remove(candle);
                }
            });

            if (!successKline.Success)
                Logger.Fatal("SubscribeToKlineUpdates {0}", successKline.Error?.Message);
        }

        public long Ping()
        {
            return Client.Ping().Data;
        }

        public AccountBinanceSymbol GetAccountBinanceSymbol()
        {
            return BinanceSymbol;
        }

        private int CalculateDecimal(decimal size)
        {
            if (size != 0.0m)
            {
                int value;
                for (value = 0; size < 1; value++)
                {
                    size *= 10;
                }

                return value;
            }

            return 0;
        }

        protected decimal ClampQuantity(decimal quantity)
        {
            return quantity.TruncateDecimal(CalculateDecimal(BinanceSymbol.LotSizeFilter.StepSize));
        }

        protected decimal ClampPrice(decimal price)
        {
            return decimal.Round(price, CalculateDecimal(BinanceSymbol.PriceFilter.TickSize));
        }

        protected void GetListenKey()
        {
            WebCallResult<string> result = UserStream.StartUserStream();
            if (result.Success)
            {
                listenKey = result.Data;
            }
            else
            {
                Logger.Fatal("{0} - GetListenKey", result.Error?.Message);
            }
        }

        protected async void KeepAliveListenKey()
        {
            while (!tokenSource.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(new TimeSpan(0, 30, 0), tokenSource.Token);

                    if (!UserStream.KeepAliveUserStream(listenKey).Success)
                    {
                        Logger.Information("KeepAliveListenKey Fail execute GetListenKey");
                        GetListenKey();
                    }
                }
                catch (Exception)
                {
                }              
            }
        }

        public void StopListenKey()
        {
            if (!string.IsNullOrWhiteSpace(listenKey)) UserStream.StopUserStream(listenKey);
        }

        /// <summary>
        /// Dispose() calls Dispose(true)
        /// </summary>
        public void Dispose()
        {
            tokenSource.Cancel();
            Client?.Dispose();
            SocketClient?.UnsubscribeAll();
            SocketClient?.Dispose();
        }
    }
}
