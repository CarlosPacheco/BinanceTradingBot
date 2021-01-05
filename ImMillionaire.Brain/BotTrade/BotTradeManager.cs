using Serilog;
using System;

namespace ImMillionaire.Brain.BotTrade
{
    public class BotTradeManager : IBotTradeManager
    {
        private ILogger Logger { get; }
        private IBotTrade BotTrade { get; }

        public BotTradeManager(ILogger logger, IBotTrade botTrade)
        {
            Logger = logger;
            BotTrade = botTrade;
        }

        public void Run()
        {
            try
            {
                BotTrade.Start();
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Host terminated unexpectedly. Trying again...");
                Run();
            }
        }
    }
}
