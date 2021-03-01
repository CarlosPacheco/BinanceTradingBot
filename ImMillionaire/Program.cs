using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
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
}

