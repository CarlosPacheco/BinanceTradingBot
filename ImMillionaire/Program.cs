using Microsoft.Extensions.DependencyInjection;
using ImMillionaire.Brain.BotTrade;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ImMillionaire
{
    class Program
    {
        static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args).ConfigureAppConfiguration((hostingContext, config) =>
             {
                 config.AddJsonFile("appsettings.json")
                 .AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName ?? "Production"}.json")
#if DEBUG
                 .AddJsonFile("appsettings.Local.Development.json", optional: true, reloadOnChange: true)
#endif
                 .AddEnvironmentVariables();
             })
            .ConfigureServices((hostingContext, services) =>
            {
                Startup.DependencyInjection(services, hostingContext.Configuration);
                services.AddHostedService<LifetimeEventsHostedService>();
            }).UseSerilog();
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
            _appLifetime.ApplicationStopping.Register(OnStopping);
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

        private void OnStopping()
        {
            _logger.LogInformation("OnStopping has been called.");

            // Perform on-stopping activities here
        }

        private void OnStopped()
        {
            _logger.LogInformation("OnStopped has been called.");

            // Perform post-stopped activities here
        }
    }
}

