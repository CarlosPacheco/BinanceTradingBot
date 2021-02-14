using System;

namespace ImMillionaire.Brain
{
    public class Utils
    {
        public static decimal TruncateDecimal(decimal value, int precision)
        {
            int step = (int)Math.Pow(10, precision);
            return Math.Truncate(step * value) / step;
        }
    }
}
