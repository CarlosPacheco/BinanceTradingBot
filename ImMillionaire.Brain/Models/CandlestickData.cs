namespace ImMillionaire.Brain
{
    public class CandlestickData
    {
        public decimal? ema10 { get; set; }

        public decimal? ema60 { get; set; }

        public decimal? ema120 { get; set; }

        public decimal? rsi14 { get; set; }
        public (decimal? LowerBand, decimal? MiddleBand, decimal? UpperBand) bb20 { get; set; }
        public decimal? ema14 { get; set; }
    }

}
