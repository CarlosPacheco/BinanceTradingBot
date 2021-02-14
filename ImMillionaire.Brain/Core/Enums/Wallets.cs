using System.Text.Json.Serialization;

namespace ImMillionaire.Brain.Core.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum WalletType
    {
        Spot,
        Margin,
        Futures
    }
}
