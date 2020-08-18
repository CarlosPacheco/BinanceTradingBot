using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot;
using Binance.Net.Objects.Spot.MarginData;
using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.Objects.Spot.MarketStream;
using Binance.Net.Objects.Spot.SpotData;
using Binance.Net.Objects.Spot.UserStream;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Trady.Analysis;
using Trady.Analysis.Extension;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain
{
    public class Trader : IDisposable
    {

        private IBinanceSocketClient SocketClient { get; set; }

        private IBinanceClient Client { get; set; }

        private string Symbol { get; set; }

        private string ApiKey { get; set; }

        private string SecretKey { get; set; }

        private IList<IOhlcv> Candlestick { get; set; }

        private IList<IOhlcv> Candlestick1Hours { get; set; }

        private IList<IOhlcv> Candlestick4Hours { get; set; }

        private IList<IOhlcv> Candlestick1Day { get; set; }

        private CandlestickData CandlestickData { get; set; }

        private Candlestick1HoursData Candlestick1HoursData { get; set; }

        //   private Candlestick4HoursData Candlestick4HoursData { get; set; }

        //  private Candlestick1DaysData Candlestick1DaysData { get; set; }

        private long Ping { get; set; }

        private BinanceExchangeInfo BinanceExchangeInfo { get; set; }

        private BinanceSymbol BinanceSymbol { get; set; }

        decimal lastBid = 0m;
        decimal lastAsk = 0m;

        BinanceStreamTrade BinanceStreamTrade;
        string listenKey;
        IBinanceBookPrice lastBookTicker = null;

        private bool BotHasStarted { get; set; }

        public Trader()
        {
            var config = JObject.Parse(File.ReadAllText("config.json"));
            /* Binance Configuration */
            Symbol = (string)config["symbol"];
            ApiKey = (string)config["key"];
            SecretKey = (string)config["secret"];

            BinanceClient.SetDefaultOptions(new BinanceClientOptions()
            {
                ApiCredentials = new ApiCredentials(ApiKey, SecretKey),
                //  LogVerbosity = LogVerbosity.Debug,
                //  LogWriters = new List<TextWriter> { Console.Out }
            });
            BinanceSocketClient.SetDefaultOptions(new BinanceSocketClientOptions()
            {
                ApiCredentials = new ApiCredentials(ApiKey, SecretKey),
                // LogVerbosity = LogVerbosity.Debug,
                //   LogWriters = new List<TextWriter> { Console.Out }
            });

            SocketClient = new BinanceSocketClient();
            Client = new BinanceClient();
        }

        public void Start()
        {
            BotHasStarted = false;
            Utils.Log("Bot started init", ConsoleColor.Green);

            GetCandlestickData();

            SocketClient.SubscribeToTradeUpdates(Symbol, (BinanceStreamTrade data) =>
            {
                BinanceStreamTrade = data;
            });

            var successDepth = SocketClient.SubscribeToOrderBookUpdates(Symbol, 100, (BinanceEventOrderBook data) =>
            {
                if (data.Bids.Any()) lastBid = data.Bids.First().Price;

                if (data.Asks.Any()) lastAsk = data.Asks.First().Price;
            });

            CandlestickData = null;
            SubscribeToKlineUpdates(Candlestick, KlineInterval.OneMinute,
            (IList<IOhlcv> candlestick, Candlestick candle) =>
            {
                CandlestickData ??= new CandlestickData();
                CandlestickData.ema120 = candlestick.Ema(120).Last().Tick;
                CandlestickData.ema60 = candlestick.Ema(60).Last().Tick;
                CandlestickData.ema10 = candlestick.Ema(10).Last().Tick;
                CandlestickData.ema14 = candlestick.Ema(14).Last().Tick;

                CandlestickData.rsi14 = candlestick.Rsi(14).Last().Tick;
                CandlestickData.bb20 = candlestick.Bb(20, 2).Last().Tick;

                Analyzable(candle.Close);
            });

            Candlestick1HoursData = null;
            SubscribeToKlineUpdates(Candlestick1Hours, KlineInterval.OneHour,
            (IList<IOhlcv> candlestick, Candlestick candle) =>
            {
                Candlestick1HoursData ??= new Candlestick1HoursData();
                Candlestick1HoursData.ema10 = candlestick.Ema(10).Last().Tick;
                Candlestick1HoursData.rsi14 = candlestick.Rsi(14).Last().Tick;
                Candlestick1HoursData.ema120 = candlestick.Ema(120).Last().Tick;
            });

            //Candlestick4HoursData = new Candlestick4HoursData();
            //SubscribeToKlineUpdates(Candlestick4Hours, KlineInterval.FourHour,
            //(IList<IOhlcv> candlestick, Candlestick candle) =>
            //{
            //    Candlestick4HoursData.ema10 = candlestick.Ema(10).Last().Tick;
            //    Candlestick4HoursData.rsi14 = candlestick.Rsi(14).Last().Tick;
            //    Candlestick4HoursData.ema60 = candlestick.Ema(60).Last().Tick;
            //});

            //Candlestick1DaysData = new Candlestick1DaysData();
            //SubscribeToKlineUpdates(Candlestick1Day, KlineInterval.OneDay,
            //(IList<IOhlcv> candlestick, Candlestick candle) =>
            //{
            //    Candlestick1DaysData.ema10 = candlestick.Ema(10).Last().Tick;
            //    Candlestick1DaysData.rsi14 = candlestick.Rsi(14).Last().Tick;
            //    Candlestick1DaysData.ema25 = candlestick.Ema(25).Last().Tick;
            //});

            GetListenKey();

            var successAccount = SocketClient.SubscribeToUserDataUpdates(listenKey,
            null,
            //(BinanceStreamAccountInfo data) =>
            //{
            //    // Handle account info data
            //},
            (BinanceStreamOrderUpdate data) =>
            {
                // Handle order update info data
                if (data.Side == OrderSide.Buy && data.Status == OrderStatus.Filled)
                {
                    decimal amount = data.Quantity;
                    if (data.Commission > 0)
                    {
                        amount = data.Quantity - (data.Quantity * 0.00075m);//BNB fee
                    }

                    SellLimit(data.Price, amount);
                }
                else if (data.Side == OrderSide.Sell && data.Status == OrderStatus.Filled)
                {
                    Utils.Log($"sell", ConsoleColor.Red);
                    BinancePlacedOrder = null;
                }
                else if (data.Side == OrderSide.Buy && data.Status == OrderStatus.Canceled)
                {
                    Utils.Log($"cancel buy", ConsoleColor.Green);
                    BinancePlacedOrder = null;
                }
            },
            null, // Handler for OCO updates
            null,
            //(IEnumerable<BinanceStreamBalance> data) =>
            //{
            //    // Handler for position updates
            //},
            null); // Handler for account balance updates (withdrawals/deposits)

            BotHasStarted = true;
            Utils.Log("Bot end init", ConsoleColor.Green);
        }

        private void GetCandlestickData()
        {
            // Public
            Ping = Client.Ping().Data;
            BinanceExchangeInfo = Client.GetExchangeInfo().Data;
            var tardeFee = Client.GetTradeFee(Symbol);
            var account = Client.GetAccountInfo();
            BinanceSymbol = BinanceExchangeInfo.Symbols.FirstOrDefault(x => x.Name == Symbol);

            // default 500 (500 minutos/ 60
            WebCallResult<IEnumerable<BinanceKline>> klines = Client.GetKlines(Symbol, KlineInterval.OneMinute);
            if (klines.Success)
            {
                Candlestick = klines.Data.Select(k => new Candlestick(k)).ToList<IOhlcv>();
            }

            Candlestick1Hours = Client.GetKlines(Symbol, KlineInterval.OneHour).Data.Select(k => new Candlestick(k)).ToList<IOhlcv>();
            // Candlestick4Hours = Client.GetKlines(Symbol, KlineInterval.FourHour).Data.Select(k => new Candlestick(k)).ToList<IOhlcv>();
            //  Candlestick1Day = Client.GetKlines(Symbol, KlineInterval.OneDay).Data.Select(k => new Candlestick(k)).ToList<IOhlcv>();

        }

        private async void GetListenKey()
        {
            WebCallResult<string> result = Client.StartMarginUserStream();
            if (result.Success)
            {
                listenKey = result.Data;

                while (true)
                {
                    await Task.Delay(new TimeSpan(0, 45, 0));

                    if (!Client.KeepAliveMarginUserStream(listenKey).Success)
                    {
                        listenKey = Client.StartMarginUserStream().Data;
                    }
                }
            }
        }

        private void SubscribeToKlineUpdates(IList<IOhlcv> candlestick, KlineInterval interval, Action<IList<IOhlcv>, Candlestick> calculateIndicators)
        {
            CallResult<CryptoExchange.Net.Sockets.UpdateSubscription> successKline = SocketClient.SubscribeToKlineUpdates(Symbol, interval, (BinanceStreamKlineData data) =>
            {
                Candlestick candle = new Candlestick(data.Data);
                candlestick.Add(candle);

                calculateIndicators(candlestick, candle);

                if (!data.Data.Final)
                {
                    candlestick.Remove(candle);
                }
            });

            if (successKline.Success)
            {
                successKline.Data.ConnectionLost += () =>
                {
                    SocketClient.Unsubscribe(successKline.Data);
                    SubscribeToKlineUpdates(candlestick, interval, calculateIndicators);
                    Utils.ErrorLog("ConnectionLost");
                };
            }
        }

        bool xptoUp = true;
        bool xptoDown = true;

        BinancePlacedOrder BinancePlacedOrder = null;
        private void Analyzable(decimal marketPrice)
        {
            if (CandlestickData.rsi14.Value > 70m)
            {
                //  Utils.ErrorLog("overbuyed sell mf");
            }
            else if (CandlestickData.rsi14.Value < 30m)
            {
                BuyLimit();

                // Utils.SuccessLog("oversell buy mf");
            }
            //up trend
            if (CandlestickData.ema120.Value < CandlestickData.ema10.Value)
            {

                if (Candlestick1HoursData?.rsi14 != null && Candlestick1HoursData.rsi14.Value > 70m)
                {
                    Utils.SuccessLog("up trend sell");
                }
                if (xptoUp)
                {
                    Utils.SuccessLog("up trend buy");
                    xptoUp = false;
                    xptoDown = true;
                }

            }
            else//down trend
            {
                if (xptoDown)
                {
                    Utils.ErrorLog("down trend");
                    xptoDown = false;
                    xptoUp = true;
                }
            }

            if (marketPrice <= CandlestickData.bb20.LowerBand)
            {
                // Utils.WarnLog("LowerBand");
            }
            else if (marketPrice >= CandlestickData.bb20.UpperBand)
            {
                //  Utils.WarnLog("UpperBand");
            }
        }

        private void BuyLimit()
        {
            if (BinancePlacedOrder == null)
            {
                WebCallResult<BinanceMarginAccount> binanceMarginAccount = Client.GetMarginAccountInfo();
                if (binanceMarginAccount.Success)
                {
                    var currentTradingAsset = binanceMarginAccount.Data.Balances.FirstOrDefault(x => x.Asset.ToLower() == "busd".ToLower());
                    if (currentTradingAsset.Free > 1)
                    {
                        var locallastBid = lastBid;

                        // margin of safe to buy in the best price 0.01%
                        decimal price = decimal.Round(locallastBid - locallastBid * (0.015m / 100), 2);

                        //decimal price = lastBid;
                        if (BinanceStreamTrade != null)
                        {
                            if (lastBid > BinanceStreamTrade.Price)
                            {
                                Utils.Log($"place buy at: {BinanceStreamTrade.Price} and {locallastBid}", ConsoleColor.DarkBlue);
                            }

                            if (price > BinanceStreamTrade.Price)
                            {
                                Utils.Log($"place buy at: {BinanceStreamTrade.Price} and {locallastBid}", ConsoleColor.DarkBlue);
                                price = decimal.Round(BinanceStreamTrade.Price - BinanceStreamTrade.Price * (0.01m / 100), 2);
                            }
                        }
                        Utils.Log($"buy Market: {BinanceStreamTrade.Price} Bid: {locallastBid} new price: {price}", ConsoleColor.DarkRed);
                        var amount = Utils.TruncateDecimal(currentTradingAsset.Free / price, 6 /*BinanceSymbol.BaseAssetPrecision*/);
                        var order = Client.PlaceMarginOrder(Symbol, OrderSide.Buy, OrderType.Limit, amount, null, null, price, TimeInForce.GoodTillCancel);
                        if (order.Success)
                        {
                            BinancePlacedOrder = order.Data;
                            CheckBuyWasExecuted();
                            //  Utils.Log($"place buy lastBookTicker: {lastBookTicker.BestBidPrice}", ConsoleColor.Green);
                            Utils.Log($"place buy at: {price}", ConsoleColor.Green);
                        }
                        else
                        {
                            Utils.Log($"error place buy at: {price} {order.Error.Message}", ConsoleColor.Red);
                        }
                    }
                }
            }
        }

        private void CheckBuyWasExecuted(int waitSecondsBeforeCancel = 25)
        {
            Task.Run(() =>
            {
                Thread.Sleep(TimeSpan.FromSeconds(waitSecondsBeforeCancel));
                if (BinancePlacedOrder == null) return;

                var orderRequest = Client.GetMarginAccountOrder(Symbol, BinancePlacedOrder.OrderId);
                if (!orderRequest.Success) return;

                BinanceOrder order = orderRequest.Data;
                if (order.Side == OrderSide.Buy && order.Status != OrderStatus.Filled)
                {
                    Client.CancelMarginOrderAsync(Symbol, BinancePlacedOrder.OrderId);
                }
            });
        }

        private async void SellLimit(decimal price, decimal amount, int numTry = 1)
        {
            if (numTry > 3) return;

            var newPrice = decimal.Round(price + price * (0.15m / 100), 2);
            try
            {
                var order = await Client.PlaceMarginOrderAsync(Symbol, OrderSide.Sell, OrderType.Limit, amount, null, null, newPrice, TimeInForce.GoodTillCancel);
                if (order.Success)
                {
                    Utils.Log($"place sell at: {newPrice}", ConsoleColor.Green);
                }
                else
                {
                    SellLimit(price, amount, numTry + 1);
                    Utils.Log($"error place sell at: {newPrice}  {order.Error.Message}", ConsoleColor.Red);
                }
            }
            catch (Exception ex)
            {
                SellLimit(price, amount, numTry + 1);
                Utils.Log($"error place sell at: {newPrice}  {ex.Message}", ConsoleColor.Red);
            }
        }

        private void CalculateIndicators(IList<IOhlcv> candlestick, Candlestick candle)
        {
            IReadOnlyList<AnalyzableTick<decimal?>> ema120 = candlestick.Ema(120);
            IReadOnlyList<AnalyzableTick<decimal?>> ema60 = candlestick.Ema(60);
            IReadOnlyList<AnalyzableTick<decimal?>> ema10 = candlestick.Ema(10);
            IReadOnlyList<AnalyzableTick<decimal?>> ema14 = candlestick.Ema(14);

            IReadOnlyList<AnalyzableTick<decimal?>> rsi14 = candlestick.Rsi(14);
            IReadOnlyList<AnalyzableTick<(decimal? LowerBand, decimal? MiddleBand, decimal? UpperBand)>> bb20 = candlestick.Bb(20, 2);

            Utils.Log($"ema120: {ema120.Last().Tick.Value}", ConsoleColor.DarkYellow);
            Utils.Log($"ema60: {ema60.Last().Tick.Value}", ConsoleColor.Blue);
            Utils.Log($"ema10: {ema10.Last().Tick.Value}", ConsoleColor.DarkGreen);
            Utils.Log($"ema14: {ema14.Last().Tick.Value}", ConsoleColor.DarkCyan);
            Utils.Log($"rsi14: {rsi14.Last().Tick.Value}", ConsoleColor.DarkMagenta);
            Utils.Log($"bb20: {bb20.Last().Tick.LowerBand}", ConsoleColor.DarkRed);

            if (rsi14.Last().Tick.Value > 70m)
            {
                Utils.ErrorLog("overbuyed sell mf");
            }
            else if (rsi14.Last().Tick.Value < 30m)
            {
                Utils.SuccessLog("oversell buy mf");
            }
            //up trend
            if (ema120.Last().Tick.Value < ema10.Last().Tick.Value)
            {
                Utils.SuccessLog("up trend");
            }
            else//down trend
            {
                Utils.ErrorLog("down trend");
            }

            if (candle.Close <= bb20.Last().Tick.LowerBand)
            {
                Utils.WarnLog("LowerBand");
            }
            else if (candle.Close >= bb20.Last().Tick.UpperBand)
            {
                Utils.WarnLog("UpperBand");
            }
        }

        public void Dispose()
        {
            Client.CloseMarginUserStream(listenKey);
            Client?.Dispose();
            SocketClient?.UnsubscribeAll();
            SocketClient?.Dispose();
        }
    }

}
