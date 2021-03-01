using ImMillionaire.Core.Enums;

namespace ImMillionaire.Core
{
    public interface IBinanceClientFactory
    {
        IBinanceClient GetBinanceClient(WalletType walletType);
    }
}