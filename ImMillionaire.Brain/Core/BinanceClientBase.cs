using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.SocketSubClient;
using Binance.Net.Interfaces.SubClients;
using Binance.Net.Objects.Spot;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using ImMillionaire.Brain.Logger;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain.Core
{
    public abstract class BinanceClientBase : IDisposable
    {
        protected IBinanceSocketClient SocketClient { get; }

        protected Binance.Net.Interfaces.IBinanceClient Client { get; }

        protected ConfigOptions Configuration { get; }

        public IBinanceClientMarket Market { get; protected set; }

        public IBinanceClientUserStream UserStream { get; protected set; }

        public IBinanceSocketClientBase BinanceSocketClientBase { get; protected set; }

        protected AccountBinanceSymbol BinanceSymbol { get; set; }

        protected string listenKey;

        public int DecimalAmount { get; set; }

        public decimal GetCurrentTradePrice { get; set; }

        public BinanceClientBase(IOptions<ConfigOptions> config)
        {
            /* Binance Configuration */
            Configuration = config.Value;

            SocketClient = new BinanceSocketClient(new BinanceSocketClientOptions()
            {
                ApiCredentials = new ApiCredentials(Configuration.ApiKey, Configuration.SecretKey),
                SocketNoDataTimeout = TimeSpan.FromMinutes(5),
                ReconnectInterval = TimeSpan.FromSeconds(1),
                // LogVerbosity = CryptoExchange.Net.Logging.LogVerbosity.Debug,
                LogWriters = new List<TextWriter> { TextWriterLogger.Out }

            });

            Client = new BinanceClient(new BinanceClientOptions()
            {
                ApiCredentials = new ApiCredentials(Configuration.ApiKey, Configuration.SecretKey),
                LogWriters = new List<TextWriter> { TextWriterLogger.Out }
                //LogVerbosity = CryptoExchange.Net.Logging.LogVerbosity.Debug,
                //AutoTimestamp = true,
                //AutoTimestampRecalculationInterval = TimeSpan.FromMinutes(30),
            });
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
                Log.Fatal("{0}", klines.Error?.Message);
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

            UpdateSubscriptionAutoConnetionIfConnLost(successKline, () => SubscribeToKlineUpdates(candlestick, interval, calculateIndicators));
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

        protected void UpdateSubscriptionAutoConnetionIfConnLost(CallResult<UpdateSubscription> updateSubscription, Action callback)
        {
            if (updateSubscription.Success)
            {
                updateSubscription.Data.ConnectionLost += () =>
                {
                    // SocketClient.Unsubscribe(updateSubscription.Data);
                    // callback();
                    Log.Fatal("ConnectionLost {0}", updateSubscription.Error?.Message);
                };

                updateSubscription.Data.Exception += (ex) =>
                {
                    Log.Fatal(ex, "ConnectionLost error unexpectedly.");
                };
            }
            else
            {
                Log.Fatal("UpdateSubscriptionAutoConnetionIfConnLost {0}", updateSubscription.Error?.Message);
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
                Log.Fatal("{0} - GetListenKey", result.Error?.Message);
            }
        }

        protected async void KeepAliveListenKey()
        {
            while (true)
            {
                await Task.Delay(new TimeSpan(0, 50, 0));

                Log.Information("Check - KeepAliveListenKey");
                if (!UserStream.KeepAliveUserStream(listenKey).Success)
                {
                    Log.Information("KeepAliveListenKey Fail execute GetListenKey");
                    GetListenKey();
                }
                else
                {
                    Log.Information("KeepAliveListenKey Success");
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
