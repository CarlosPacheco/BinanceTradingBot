using ImMillionaire.Brain;
using System;

namespace ImMillionaire
{
    class Program
    {
        static void Main(string[] args)
        {
            StartAll();
        }

        private static void StartAll()
        {
            try
            {  
                using (MyBotTrade trader = new MyBotTrade())
                //using (Trader trader = new Trader())
                // using (TraderFutures traderFuture = new TraderFutures())
                {
                    trader.Start();
                 //   traderFuture.Start();

                    Console.ReadLine();
                }
            }
            catch (Exception ex)
            {
                StartAll();
            }
         
        }
    }
}
