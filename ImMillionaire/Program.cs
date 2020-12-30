using ImMillionaire.Brain;
using ImMillionaire.Brain.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.IO;
using Microsoft.Extensions.Options;

namespace ImMillionaire
{
    class Program
    {
        /// <summary>
        /// Logger instance
        /// </summary>
        //protected readonly ILogger Logger;

        static void Main(string[] args)
        {
            // Create service collection and configure our services
            ServiceCollection services = new ServiceCollection();

            IConfiguration Configuration = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json")
#if PROD
        .AddJsonFile("appsettings.Production.json", optional: false, reloadOnChange: true);
#else // DEBUG
        .AddJsonFile("appsettings.Development.json")
        .AddJsonFile("appsettings.Local.Development.json", optional: true)
#endif
        .Build();

            services.AddSingleton(config => Configuration);
            // IoC Logger 
            ILogger logger = Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(Configuration).CreateLogger();
            services.AddSingleton(logger);
            // Add functionality to inject IOptions<T>
            services.AddOptions();

            // Add our Config object so it can be injected
            services.Configure<ConfigOptions>(Configuration.GetSection(ConfigOptions.Position));
            // Generate a provider
            IServiceProvider serviceProvider = services.BuildServiceProvider();
            IOptions<ConfigOptions> config = serviceProvider.GetService<IOptions<ConfigOptions>>();
            StartAll(logger, config);
        }

        private static void StartAll(ILogger logger, IOptions<ConfigOptions> config)
        {
            try
            {
                //  using (BaseBot trader = new MyBotTradeConservative())
                using BaseBot trader = new MyBotTrade(config);
                trader.Start();
                //   traderFuture.Start();

                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly. Trying again...");

                StartAll(logger, config);
            }

        }
    }
}
