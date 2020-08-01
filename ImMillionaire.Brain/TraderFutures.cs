using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Futures;
using Binance.Net.Objects.Futures.FuturesData;
using Binance.Net.Objects.Futures.MarketData;
using Binance.Net.Objects.Futures.MarketStream;
using Binance.Net.Objects.Futures.UserStream;
using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.Objects.Spot.MarketStream;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Trady.Analysis.Extension;
using Trady.Core.Infrastructure;

namespace ImMillionaire.Brain
{
    public class TraderFutures : IDisposable
    {

        private IBinanceFuturesSocketClient SocketClient { get; set; }

        private IBinanceFuturesClient Client { get; set; }

        private string Symbol { get; set; }

        private string ApiKey { get; set; }

        private string SecretKey { get; set; }

        private IList<IOhlcv> Candlestick { get; set; }

        private IList<IOhlcv> Candlestick1Hours { get; set; }

        private IList<IOhlcv> Candlestick4Hours { get; set; }

        private IList<IOhlcv> Candlestick1Day { get; set; }

        private CandlestickData CandlestickData { get; set; }

        private Candlestick1HoursData Candlestick1HoursData { get; set; }

        private Candlestick4HoursData Candlestick4HoursData { get; set; }

        private Candlestick1DaysData Candlestick1DaysData { get; set; }

        private long Ping { get; set; }

        private BinanceFuturesExchangeInfo BinanceExchangeInfo { get; set; }

        private BinanceFuturesSymbol BinanceFuturesSymbol { get; set; }

        decimal lastBid = 0m;
        decimal lastAsk = 0m;
        string listenKey;
        IBinanceBookPrice lastBookTicker = null;

        public TraderFutures()
        {
            var config = JObject.Parse(File.ReadAllText("config.json"));
            /* Binance Configuration */
            Symbol = (string)config["symbol"];
            ApiKey = (string)config["key"];
            SecretKey = (string)config["secret"];

            BinanceFuturesClient.SetDefaultOptions(new BinanceFuturesClientOptions()
            {
                ApiCredentials = new ApiCredentials(ApiKey, SecretKey),
                //  LogVerbosity = LogVerbosity.Debug,
                //  LogWriters = new List<TextWriter> { Console.Out }
            });
            BinanceFuturesSocketClient.SetDefaultOptions(new BinanceFuturesSocketClientOptions()
            {
                ApiCredentials = new ApiCredentials(ApiKey, SecretKey),
                // LogVerbosity = LogVerbosity.Debug,
                //   LogWriters = new List<TextWriter> { Console.Out }
            });

            SocketClient = new BinanceFuturesSocketClient();
            Client = new BinanceFuturesClient();
        }

        bool onetime = true;
        bool onetime1 = true;
        bool onetime2 = true;
        public void Start()
        {
            GetCandlestickData();

            var successBooks = SocketClient.SubscribeToBookTickerUpdates(Symbol, (BinanceStreamBookPrice data) => lastBookTicker = data);

            var successSingleTicker = SocketClient.SubscribeToSymbolTickerUpdates(Symbol, (BinanceStreamTick data) =>
            {
                if (BinancePlacedOrder != null)
                {
                    if (onetime2)
                    {
                        Console.WriteLine(DateTime.Now.ToString() + " | INFO: " + $"LastPrice: {data.LastPrice}--- AskPrice {data.AskPrice}--- BidPrice {data.BidPrice}");
                        onetime2 = false;
                    }
                }
                else
                {
                    onetime2 = true;
                }
            
                // handle data
            });

            var successTrades = SocketClient.SubscribeToMarkPriceUpdates(Symbol, 1000, (BinanceFuturesStreamMarkPrice data) =>
            {
                if(BinancePlacedOrder != null)
                {
                    if(onetime)
                    {
                        Console.WriteLine(DateTime.Now.ToString() + " | INFO: " + $"event: {data.Event} Price: {data.MarkPrice}---");
                        onetime = false;
                    }                      
                }
                else
                {
                    onetime = true;
                }

                // handle data
            });

            var successDepth = SocketClient.SubscribeToOrderBookUpdates(Symbol, 500, (BinanceFuturesStreamOrderBookDepth data) =>
            {
                if (data.Bids.Any()) lastBid = data.Bids.First().Price;

                if (data.Asks.Any()) lastAsk = data.Asks.First().Price;

                if (BinancePlacedOrder != null)
                {
                    if (onetime1)
                    {
                        Console.WriteLine(DateTime.Now.ToString() + " | " + $"Asks: {data.Asks.First().Price} Bids: {data.Bids.First().Price}");
                        onetime1 = false;
                    }
                }
                else
                {
                    onetime1 = true;
                }
                // Console.WriteLine(DateTime.Now.ToString() + " | " + $"Asks: {data.Asks.First().Price} Bids: {data.Bids.First().Price}");

                // handle data
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
                // CalculateIndicators(candlestick, candle);
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

            Candlestick4HoursData = new Candlestick4HoursData();
            SubscribeToKlineUpdates(Candlestick4Hours, KlineInterval.FourHour,
            (IList<IOhlcv> candlestick, Candlestick candle) =>
            {
                Candlestick4HoursData.ema10 = candlestick.Ema(10).Last().Tick;
                Candlestick4HoursData.rsi14 = candlestick.Rsi(14).Last().Tick;
                Candlestick4HoursData.ema60 = candlestick.Ema(60).Last().Tick;
            });

            Candlestick1DaysData = new Candlestick1DaysData();
            SubscribeToKlineUpdates(Candlestick1Day, KlineInterval.OneDay,
            (IList<IOhlcv> candlestick, Candlestick candle) =>
            {
                Candlestick1DaysData.ema10 = candlestick.Ema(10).Last().Tick;
                Candlestick1DaysData.rsi14 = candlestick.Rsi(14).Last().Tick;
                Candlestick1DaysData.ema25 = candlestick.Ema(25).Last().Tick;
            });

            GetListenKey();

            var successAccount = SocketClient.SubscribeToUserDataUpdates(listenKey,
            null,
            null,
            null, // Handler for OCO updates
            null,
            (BinanceFuturesStreamOrderUpdate data) =>
            {
                // Handle order update info data
                if (data.Side == OrderSide.Buy && data.Status == OrderStatus.Filled)
                {
                    Utils.Log($"buy future", ConsoleColor.Green);
                    //  BinancePlacedOrder = null;
                    SellLimit(data.Price, data.Quantity);
                }
                else if (data.Side == OrderSide.Sell && data.Status == OrderStatus.Filled)
                {
                    Utils.Log($"sell future", ConsoleColor.Red);
                    BinancePlacedOrder = null;
                }
                else if (data.Side == OrderSide.Buy && data.Status == OrderStatus.Canceled)
                {
                    Utils.Log($"cancel buy future", ConsoleColor.Green);
                    BinancePlacedOrder = null;
                }
            },
            null); // Handler for account balance updates (withdrawals/deposits)

            Utils.Log("Bot futures started", ConsoleColor.Green);
        }

        private void GetCandlestickData()
        {
            // Public
            Ping = Client.Ping().Data;
            BinanceExchangeInfo = Client.GetExchangeInfo().Data;

            BinanceFuturesSymbol = BinanceExchangeInfo.Symbols.FirstOrDefault(x => x.Name == Symbol);

            // default 500 (500 minutos/ 60
            WebCallResult<IEnumerable<BinanceKline>> klines = Client.GetKlines(Symbol, KlineInterval.OneMinute);
            if (klines.Success)
            {
                Candlestick = klines.Data.Select(k => new Candlestick(k)).ToList<IOhlcv>();
            }

            Candlestick1Hours = Client.GetKlines(Symbol, KlineInterval.OneHour).Data.Select(k => new Candlestick(k)).ToList<IOhlcv>();
            Candlestick4Hours = Client.GetKlines(Symbol, KlineInterval.FourHour).Data.Select(k => new Candlestick(k)).ToList<IOhlcv>();
            Candlestick1Day = Client.GetKlines(Symbol, KlineInterval.OneDay).Data.Select(k => new Candlestick(k)).ToList<IOhlcv>();
        }

        private async void GetListenKey()
        {
            listenKey = Client.StartUserStream().Data;
            while (true)
            {
                await Task.Delay(new TimeSpan(0, 45, 0));

                var keepAliveResult = Client.KeepAliveUserStream(listenKey);
                if (!keepAliveResult.Success)
                {
                    listenKey = Client.StartUserStream().Data;
                }
            }
        }

        private void SubscribeToKlineUpdates(IList<IOhlcv> candlestick, KlineInterval interval, Action<IList<IOhlcv>, Candlestick> calculateIndicators)
        {
            CallResult<CryptoExchange.Net.Sockets.UpdateSubscription> successKline = SocketClient.SubscribeToKlineUpdates(Symbol, interval, (BinanceStreamKline data) =>
            {
                Candlestick candle = new Candlestick(data);
                candlestick.Add(candle);

                calculateIndicators(candlestick, candle);

                if (!data.Final)
                {
                    candlestick.Remove(candle);
                }
                // handle data
            });

            if (successKline.Success)
            {
                successKline.Data.ConnectionLost += () =>
                {
                    SocketClient.Unsubscribe(successKline.Data);
                    SubscribeToKlineUpdates(candlestick, interval, calculateIndicators);
                    // successKline.Data.Exception
                    Utils.ErrorLog("ConnectionLost");
                };
            }
        }

        bool? xptoUp = null;
        bool? xptoDown = null;
        BinanceFuturesPlacedOrder BinancePlacedOrder = null;
        DateTime lastDateUp = DateTime.UtcNow;
        DateTime lastDateDown = DateTime.UtcNow;
        private void Analyzable(decimal marketPrice)
        {
            if (CandlestickData.rsi14.Value > 70m)
            {
               // SellNow();
                //  Utils.ErrorLog("overbuyed sell mf");
            }
            else if (CandlestickData.rsi14.Value < 30m)
            {

                // Utils.SuccessLog("oversell buy mf");
            }
            //up trend
            if (CandlestickData.ema120.Value < CandlestickData.ema10.Value)
            {
                lastDateUp = DateTime.UtcNow;
                if ((DateTime.UtcNow - lastDateDown).TotalSeconds > 10)
                {
                    if (Candlestick1HoursData?.rsi14 != null && Candlestick1HoursData.rsi14.Value > 70m)
                    {
                        Utils.Log("up trend sell future", ConsoleColor.Green);
                    }

                    if (xptoUp.HasValue && xptoUp == true)
                    {
                        BuyLimit();
                        Utils.Log("up trend buy future", ConsoleColor.Green);
                        xptoUp = false;
                        xptoDown = true;
                    }
                    else
                    {
                        xptoDown = true;
                    }
                }
            }
            else//down trend
            {
                lastDateDown = DateTime.UtcNow;
                if ((DateTime.UtcNow - lastDateUp).TotalSeconds > 10)
                {
                    if (xptoDown.HasValue && xptoDown == true)
                    {
                      //  SellNow();
                        Utils.ErrorLog("down trend future");
                        xptoDown = false;
                        xptoUp = true;
                    }
                    else
                    {
                        xptoUp = true;
                    }
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
                WebCallResult<BinanceFuturesAccountInfo> binanceAccount = Client.GetAccountInfo();
                if (binanceAccount.Success)
                {
                    var currentTradingAsset = binanceAccount.Data.Assets.FirstOrDefault(x => x.Asset.ToLower() == "USDT".ToLower());
                    var currentTradingPosition = binanceAccount.Data.Positions.FirstOrDefault(x => x.Symbol == Symbol);

                    if (currentTradingAsset.MaxWithdrawAmount > 1)
                    {
                        if (lastBookTicker == null) return;

                        decimal price = lastBookTicker.BestBidPrice;
                        
                        var amount = decimal.Round(currentTradingAsset.MaxWithdrawAmount / price * currentTradingPosition.Leverage * 0.97m, BinanceFuturesSymbol.QuantityPrecision);
                        var order = Client.PlaceOrder(Symbol, OrderSide.Buy, OrderType.Limit, amount, PositionSide.Both, TimeInForce.GoodTillCancel, null, price);
                        if (order.Success)
                        {
                            BinancePlacedOrder = order.Data;
                            CheckBuyWasExecuted();
                            Utils.Log($"future place buy at: {price} {lastBookTicker.BestAskPrice} ", ConsoleColor.Green);
                        }
                        else
                        {
                            Utils.Log($"future error place buy at: {price} {amount} {order.Error.Message}", ConsoleColor.Red);
                        }
                    }
                }
            }
        }

        private async void CheckBuyWasExecuted(int waitMinutesBeforeCancel = 1)
        {
            await Task.Delay(new TimeSpan(0, waitMinutesBeforeCancel, 0));
            if (BinancePlacedOrder == null) return;

            var orderRequest = Client.GetOrder(Symbol, BinancePlacedOrder.OrderId);
            if (!orderRequest.Success) return;

            BinanceFuturesOrder order = orderRequest.Data;
            if (order.Side == OrderSide.Buy && order.Status != OrderStatus.Filled)
            {
                Client.CancelOrder(Symbol, BinancePlacedOrder.OrderId);
            }
        }

        private void SellNow()
        {
            if (BinancePlacedOrder == null) return;

            if (lastBookTicker == null) return;

            decimal price = lastBookTicker.BestAskPrice;

            SellOrder(price, BinancePlacedOrder.ExecutedQuantity);
        }

        private void SellLimit(decimal price, decimal amount)
        {
            price = decimal.Round(price + price * (0.10m / 100), 2);
            SellOrder(price, amount);
        }

        private void SellOrder(decimal price, decimal amount)
        {
            var order = Client.PlaceOrder(Symbol, OrderSide.Sell, OrderType.Limit, amount, PositionSide.Both, TimeInForce.GoodTillCancel, null, price);
            if (order.Success)
            {
                Utils.Log($"future place sell at: {price}", ConsoleColor.Green);
            }
            else
            {
                Utils.Log($"future error place sell at: {price}  {order.Error.Message}", ConsoleColor.Red);
            }
        }

        public void Dispose()
        {
            Client.StopUserStream(listenKey);
            Client?.Dispose();
            SocketClient?.UnsubscribeAll();
            SocketClient?.Dispose();
        }

    }

}
