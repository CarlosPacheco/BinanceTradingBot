using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot;
using CryptoExchange.Net.Authentication;
using ImMillionaire.Brain;
using ImMillionaire.Brain.BotTrade;
using ImMillionaire.Brain.Core;
using ImMillionaire.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;

namespace ImMillionaire
{
    public class Startup
    {
        public static void DependencyInjection(IServiceCollection services, IConfiguration Configuration)
        {
            services.AddSingleton(config => Configuration);
            // IoC Logger 
            Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(Configuration).CreateLogger();
            services.AddSingleton<TextWriterLogger>();
            ConfigOptions config = Configuration.GetSection(ConfigOptions.Position).Get<ConfigOptions>();

            // Add functionality to inject IOptions<T>
            services.AddOptions();

            // Add our Config object so it can be injected
            services.Configure<ConfigOptions>(Configuration.GetSection(ConfigOptions.Position));

            services.AddSingleton<IBotTradeManager, BotTradeManager>();
            services.AddSingleton<IBinanceClientFactory, BinanceClientFactory>();

            services.AddTransient<IBinanceSocketClient>(serviceProvider =>
             new BinanceSocketClient(new BinanceSocketClientOptions()
             {
                 ApiCredentials = new ApiCredentials(config.ApiKey, config.SecretKey),
                 ReconnectInterval = TimeSpan.FromSeconds(1),
                 LogWriters = new List<TextWriter> { serviceProvider.GetService<TextWriterLogger>() },
#if RELEASE
                 LogVerbosity = CryptoExchange.Net.Logging.LogVerbosity.Error,
#endif
             }));

            services.AddTransient<Binance.Net.Interfaces.IBinanceClient>(serviceProvider =>
            new BinanceClient(new BinanceClientOptions()
            {
                ApiCredentials = new ApiCredentials(config.ApiKey, config.SecretKey),
                LogWriters = new List<TextWriter> { serviceProvider.GetService<TextWriterLogger>() },
                TradeRulesBehaviour = TradeRulesBehaviour.AutoComply,
#if RELEASE
                LogVerbosity = CryptoExchange.Net.Logging.LogVerbosity.Error,
#endif
            }));

            services.AddTransient<IBotTrade, MyBotTrade>();
        }
    }
}
