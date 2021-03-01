using System.Text.Json.Serialization;

namespace ImMillionaire.Core.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum WalletType
    {
        Spot,
        Margin,
        Futures
    }
}
