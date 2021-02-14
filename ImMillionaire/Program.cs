using ImMillionaire.Brain.Core;
using Microsoft.Extensions.DependencyInjection;
using ImMillionaire.Brain.BotTrade;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace ImMillionaire
{
    class Program
    {
        static void Main(string[] args)
        {
            CreateHostBuilder(args).Build()
                .Services.GetService<IBotTradeManager>().Run();
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
            .ConfigureServices((hostingContext, services) => ContainerBuilder.DependencyInjection(services, hostingContext.Configuration));
    }
}

