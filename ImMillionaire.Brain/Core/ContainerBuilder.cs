using ImMillionaire.Brain.BotTrade;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.IO;

namespace ImMillionaire.Brain.Core
{
    public class ContainerBuilder
    {
        public static IServiceProvider Build()
        {
            // Create service collection and configure our services
            ServiceCollection services = new ServiceCollection();

            IConfiguration Configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
#if PROD
        .AddJsonFile("appsettings.Production.json", optional: false, reloadOnChange: true);//{env.EnvironmentName}
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

            services.AddSingleton<IBotTradeManager, BotTradeManager>();
            services.AddSingleton<IBotTrade, MyBotTrade>();
            // services.AddSingleton<IBotTrade, MyBotTradeConservative>();

            // Generate a provider
            return services.BuildServiceProvider();
        }
    }
}
