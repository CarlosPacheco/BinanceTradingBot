using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces.Clients;
using CryptoExchange.Net.Authentication;
using ImMillionaire.Brain;
using ImMillionaire.Brain.BotTrade;
using ImMillionaire.Brain.Core;
using ImMillionaire.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Threading;
using System.Threading.Tasks;

namespace ImMillionaire
{
    public class Startup
    {
        public static void DependencyInjection(IServiceCollection services, IConfiguration Configuration)
        {
            services.AddSingleton(config => Configuration);
            // IoC Logger 
            services.AddSingleton(Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(Configuration).CreateLogger());

            ConfigOptions config = Configuration.GetSection(ConfigOptions.Position).Get<ConfigOptions>();

            // Add functionality to inject IOptions<T>
            services.AddOptions();

            // Add our Config object so it can be injected
            services.Configure<ConfigOptions>(Configuration.GetSection(ConfigOptions.Position));

            services.AddSingleton<IBotTradeManager, BotTradeManager>();
            services.AddSingleton<IBinanceClientFactory, BinanceClientFactory>();

            services.AddTransient<IBinanceSocketClient>(serviceProvider =>
             new BinanceSocketClient(options =>
             {
                 options.ApiCredentials = new ApiCredentials(config.ApiKey, config.SecretKey);
             }));

            services.AddTransient<IBinanceRestClient>(serviceProvider =>
            new BinanceRestClient(options =>
            {
                options.ApiCredentials = new ApiCredentials(config.ApiKey, config.SecretKey);
                options.Environment = Binance.Net.BinanceEnvironment.Live;

                options.SpotOptions.TradeRulesBehaviour = TradeRulesBehaviour.AutoComply;
                options.SpotOptions.AutoTimestamp = true;
                options.UsdFuturesOptions.TradeRulesBehaviour = TradeRulesBehaviour.AutoComply;
                options.UsdFuturesOptions.AutoTimestamp = true;
                
            }));

            services.AddTransient<IBotTrade, MyBotTrade>();
        }
    }

    internal class LifetimeEventsHostedService : IHostedService
    {
        private readonly ILogger<LifetimeEventsHostedService> _logger;
        private readonly IHostApplicationLifetime _appLifetime;

        private readonly IBotTradeManager _botTradeManager;

        public LifetimeEventsHostedService(ILogger<LifetimeEventsHostedService> logger, IBotTradeManager botTradeManager, IHostApplicationLifetime appLifetime)
        {
            _logger = logger;
            _botTradeManager = botTradeManager;
            _appLifetime = appLifetime;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _appLifetime.ApplicationStarted.Register(OnStarted);
            _appLifetime.ApplicationStopped.Register(OnStopped);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private void OnStarted()
        {
            _logger.LogInformation("OnStarted has been called.");
            _botTradeManager.Run();
            // Perform post-startup activities here
        }

        private void OnStopped()
        {
            _logger.LogInformation("OnStopped has been called.");

            // Perform post-stopped activities here
        }
    }
}
