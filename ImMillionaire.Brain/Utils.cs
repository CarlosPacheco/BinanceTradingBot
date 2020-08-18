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

        public static void ErrorLog(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(DateTime.Now.ToString() + " | ERROR: " + msg);
            Console.ResetColor();
            Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
        }

        public static void WarnLog(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(DateTime.Now.ToString() + " | WARN: " + msg);
            Console.ResetColor();
            Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
        }

        public static void SuccessLog(string msg)
        {
            Log("SUCCESS: " + msg, ConsoleColor.Green);
        }

        public static void Log(string msg, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(DateTime.Now.ToString() + " | " + msg);
            Console.ResetColor();
            Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
        }

        public static void InfoLog(string msg)
        {
            Console.WriteLine(DateTime.Now.ToString() + " | INFO: " + msg);
            Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
        }
    }
}
