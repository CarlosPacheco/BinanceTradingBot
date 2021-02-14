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
using System.Threading.Tasks;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain.Core
{
    public abstract class BinanceClientBase : IDisposable
    {
        protected ILogger Logger { get; }

        protected IBinanceSocketClient SocketClient { get; }

        protected Binance.Net.Interfaces.IBinanceClient Client { get; }

        public IBinanceClientMarket Market { get; protected set; }

        public IBinanceClientUserStream UserStream { get; protected set; }

        public IBinanceSocketClientBase BinanceSocketClientBase { get; protected set; }

        protected AccountBinanceSymbol BinanceSymbol { get; set; }

        protected string listenKey;

        public int DecimalAmount { get; set; }

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

        protected void CalculateDecimalAmount(decimal stepSize)
        {
            if (stepSize != 0.0m)
            {
                for (DecimalAmount = 0; stepSize < 1; DecimalAmount++)
                {
                    stepSize *= 10;
                }
            }
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
            while (true)
            {
                await Task.Delay(new TimeSpan(0, 50, 0));

                if (!UserStream.KeepAliveUserStream(listenKey).Success)
                {
                    Logger.Information("KeepAliveListenKey Fail execute GetListenKey");
                    GetListenKey();
                }
            }
        }

        public void Dispose()
        {
            if (!string.IsNullOrWhiteSpace(listenKey)) UserStream.StopUserStream(listenKey);
            Client?.Dispose();
            SocketClient?.UnsubscribeAll();
            SocketClient?.Dispose();
        }
    }
}
