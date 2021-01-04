using ImMillionaire.Brain.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using ImMillionaire.Brain.BotTrade;

namespace ImMillionaire
{
    class Program
    {
        public static readonly IServiceProvider serviceProvider = ContainerBuilder.Build();

        static void Main(string[] args)
        {
            IBotTradeManager botTradeManager = serviceProvider.GetService<IBotTradeManager>();
            botTradeManager.Start();
        }
    }
}
