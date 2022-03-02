using Binance.Net.Interfaces.Clients;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ImMillionaire.Core
{
    public abstract class BinanceClientBase : IDisposable
    {
        protected ILogger<BinanceClientBase> Logger { get; }

        protected IBinanceSocketClient SocketClient { get; }

        protected Binance.Net.Interfaces.Clients.IBinanceClient Client { get; }

        protected CancellationTokenSource tokenSource = new CancellationTokenSource();

        protected AccountBinanceSymbol BinanceSymbol { get; set; }

        protected string listenKey;

        public decimal GetCurrentTradePrice { get; set; }

        public BinanceClientBase(IBinanceSocketClient socketClient, Binance.Net.Interfaces.Clients.IBinanceClient client, ILogger<BinanceClientBase> logger)
        {
            SocketClient = socketClient;
            Client = client;
            Logger = logger;
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

        protected void GetListenKey(Task<WebCallResult<string>> taskStartUser)
        {
            WebCallResult<string> result = taskStartUser.Result;
            if (result.Success)
            {
                listenKey = result.Data;
            }
            else
            {
                Logger.LogCritical("{0} - GetListenKey", result.Error?.Message);
            }
        }

        protected async void KeepAliveListenKey(Task<WebCallResult<object>> taskKeepAlive, Task<WebCallResult<string>> taskStartUser)
        {
            while (!tokenSource.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(new TimeSpan(0, 30, 0), tokenSource.Token);

                    if (!taskKeepAlive.Result.Success)
                    {
                        Logger.LogInformation("KeepAliveListenKey Fail execute GetListenKey");
                        GetListenKey(taskStartUser);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        public void StopListenKey(Task<WebCallResult<object>> taskStopUser)
        {
            if (!string.IsNullOrWhiteSpace(listenKey)) _ = taskStopUser;
        }

        /// <summary>
        /// Dispose() calls Dispose(true)
        /// </summary>
        public void Dispose()
        {
            tokenSource.Cancel();
            Client?.Dispose();
            SocketClient?.UnsubscribeAllAsync().Wait();
            SocketClient?.Dispose();
        }
    }
}
