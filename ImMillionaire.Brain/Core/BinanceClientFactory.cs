using ImMillionaire.Brain.Core.Enums;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace ImMillionaire.Brain.Core
{
    public class BinanceClientFactory : IBinanceClientFactory
    {
        private IServiceProvider Provider { get; }

        public BinanceClientFactory(IServiceProvider provider)
        {
            Provider = provider;
        }

        public IBinanceClient GetBinanceClient(WalletType walletType)
        {
            //binance client factory
            switch (walletType)
            {
                case WalletType.Spot:
                    return ActivatorUtilities.CreateInstance<BinanceClientSpot>(Provider);
                case WalletType.Margin:
                    return ActivatorUtilities.CreateInstance<BinanceClientMargin>(Provider);
                case WalletType.Futures:
                    return ActivatorUtilities.CreateInstance<BinanceClientFutures>(Provider);
            }

            return null;
        }
    }
}