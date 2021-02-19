using Binance.Net;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot;
using CryptoExchange.Net.Authentication;
using ImMillionaire.Brain.BotTrade;
using ImMillionaire.Brain.Logger;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;

namespace ImMillionaire.Brain.Core
{
    public class ContainerBuilder
    {
        public static void DependencyInjection(IServiceCollection services, IConfiguration Configuration)
        {
            services.AddSingleton(config => Configuration);
            // IoC Logger 
            services.AddSingleton<ILogger>(Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(Configuration).CreateLogger());
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
                 SocketNoDataTimeout = TimeSpan.FromMinutes(5),
                 ReconnectInterval = TimeSpan.FromSeconds(1),
                 LogVerbosity = CryptoExchange.Net.Logging.LogVerbosity.Error,
                 LogWriters = new List<TextWriter> { serviceProvider.GetService<TextWriterLogger>() }
             }));

            services.AddTransient<Binance.Net.Interfaces.IBinanceClient>(serviceProvider =>
            new BinanceClient(new BinanceClientOptions()
            {
                ApiCredentials = new ApiCredentials(config.ApiKey, config.SecretKey),
                LogWriters = new List<TextWriter> { serviceProvider.GetService<TextWriterLogger>() },
                LogVerbosity = CryptoExchange.Net.Logging.LogVerbosity.Error,
                //AutoTimestamp = true,
                //AutoTimestampRecalculationInterval = TimeSpan.FromMinutes(30),
            }));

            services.AddSingleton<IBotTrade, MyBotTrade>();
            // services.AddSingleton<IBotTrade, MyBotTradeConservative>();
        }
    }
}
