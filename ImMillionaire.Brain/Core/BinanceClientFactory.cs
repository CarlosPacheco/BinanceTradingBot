﻿using ImMillionaire.Brain.Core.Enums;
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
            return walletType switch
            {
                WalletType.Spot => ActivatorUtilities.CreateInstance<BinanceClientSpot>(Provider),
                WalletType.Margin => ActivatorUtilities.CreateInstance<BinanceClientMargin>(Provider),
                WalletType.Futures => ActivatorUtilities.CreateInstance<BinanceClientFutures>(Provider),
                _ => null,
            };
        }
    }
}