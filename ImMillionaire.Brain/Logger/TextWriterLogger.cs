using Serilog;
using System.IO;
using System.Text;

namespace ImMillionaire.Brain.Logger
{
    public class TextWriterLogger : TextWriter
    {
        private ILogger Logger;
        private StringBuilder builder = new StringBuilder();
        private bool terminatorStarted = false;

        public TextWriterLogger(ILogger logger)
        {
            Logger = logger;
        }

        public override void Write(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            Logger.Debug(value);
        }

        public override void Write(char value)
        {
            builder.Append(value);
            if (value == NewLine[0])
            {
                if (NewLine.Length == 1)
                {
                    Flush2Log();
                }
                else
                {
                    terminatorStarted = true;
                }
            }
            else if (terminatorStarted && (terminatorStarted = NewLine[1] == value))
            {
                Flush2Log();
            }
        }

        private void Flush2Log()
        {
            if (builder.Length > NewLine.Length)
            {
                Logger.Debug(builder.ToString());
            }

            builder.Clear();
            terminatorStarted = false;
        }


        public override Encoding Encoding
        {
            get { return Encoding.Default; }
        }
    }
}
