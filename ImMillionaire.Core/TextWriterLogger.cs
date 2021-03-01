using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;

namespace ImMillionaire.Core
{
    public class TextWriterLogger : TextWriter
    {
        private readonly ILogger<TextWriterLogger> Logger;
        private readonly StringBuilder builder = new StringBuilder();
        private bool terminatorStarted = false;

        public TextWriterLogger(ILogger<TextWriterLogger> logger)
        {
            Logger = logger;
        }

        public override void Write(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            Logger.LogDebug(value);
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
                Logger.LogDebug(builder.ToString());
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
