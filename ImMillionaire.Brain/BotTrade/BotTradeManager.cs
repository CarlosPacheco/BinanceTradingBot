using ImMillionaire.Brain.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ImMillionaire.Brain.BotTrade
{
    public class BotTradeManager : IBotTradeManager
    {
        private ILogger Logger { get; }
        private IBotTrade BotTrade { get; }
        private IList<IBotTrade> Bots { get; }

        public BotTradeManager(ILogger logger, IBotTrade botTrade)
        {
            Logger = logger;
            BotTrade = botTrade;
        }

        public void Run()
        {
            try
            {
                IList<Bot> bots = JsonSerializer.Deserialize<IList<Bot>>(File.ReadAllText("bots.json"));
                //BotTrade.Start(bots.First());
                //Console.ReadLine();
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Host terminated unexpectedly. Trying again...");
                Run();
            }
        }
    }
}
