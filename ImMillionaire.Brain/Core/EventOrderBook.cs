namespace ImMillionaire.Brain.Core
{
    public class EventOrderBook
    {
        public decimal LastBidPrice { get; set; }

        public decimal LastAskPrice { get; set; }

        public EventOrderBook(decimal lastAskPrice, decimal lastBidPrice)
        {
            LastAskPrice = lastAskPrice;
            LastBidPrice = lastBidPrice;
        }
    }
}
