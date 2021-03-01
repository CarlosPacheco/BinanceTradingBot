using ImMillionaire.Brain.Core;
using ImMillionaire.Brain.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ImMillionaire.Brain.BotTrade
{
    public class BotTradeManager : IBotTradeManager
    {
        private ILogger<BotTradeManager> Logger { get; }
        private IBotTrade BotTrade { get; }
        private IList<IBotTrade> Bots { get; }

        public BotTradeManager(ILogger<BotTradeManager> logger, IBotTrade botTrade)
        {
            Logger = logger;
            BotTrade = botTrade;
        }

        public void Run()
        {
            try
            {
                IList<Bot> bots = JsonSerializer.Deserialize<IList<Bot>>(File.ReadAllText("bots.json"));
                BotTrade.Start(bots.First());
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "Host terminated unexpectedly. Trying again...");
                Run();
            }
        }
    }
}
