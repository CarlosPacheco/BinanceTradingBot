using ImMillionaire.Brain.Core.Enums;

namespace ImMillionaire.Brain.Core
{
    public interface IBinanceClientFactory
    {
        IBinanceClient GetBinanceClient(WalletType walletType);
    }
}