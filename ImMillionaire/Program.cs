using ImMillionaire.Brain;
using ImMillionaire.Brain.Core;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.IO;

namespace ImMillionaire
{
    class Program
    {
        static void Main(string[] args)
        {
            IConfiguration configuration = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json").Build();
            Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger();

            StartAll();
        }

        private static void StartAll()
        {
            try
            {
               //  using (BaseBot trader = new MyBotTradeConservative())
                using (BaseBot trader = new MyBotTrade())
                // using (BaseBot traderFuture = new TraderFutures())
                {
                    trader.Start();
                 //   traderFuture.Start();

                    Console.ReadLine();
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly.");

                StartAll();

                Log.Information("Trying again...");
            }
         
        }
    }
}
