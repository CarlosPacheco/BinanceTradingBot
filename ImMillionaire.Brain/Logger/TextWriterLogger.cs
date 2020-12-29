using Serilog;
using System.IO;
using System.Text;

namespace ImMillionaire.Brain.Logger
{
    public class TextWriterLogger : TextWriter
    {
        private ILogger logger;
        private StringBuilder builder = new StringBuilder();
        private bool terminatorStarted = false;

        public TextWriterLogger()
        {
            logger = Log.Logger;
        }

        public static TextWriter Out
        {
            get
            {
                return new TextWriterLogger();
            }
        }

        public override void Write(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            logger.Debug(value);
        }

        public override void Write(char value)
        {
            builder.Append(value);
            if (value == NewLine[0])
                if (NewLine.Length == 1)
                    Flush2Log();
                else
                    terminatorStarted = true;
            else if (terminatorStarted)
                if (terminatorStarted = NewLine[1] == value)
                    Flush2Log();
        }

        private void Flush2Log()
        {
            if (builder.Length > NewLine.Length)
                logger.Debug(builder.ToString());
            builder.Clear();
            terminatorStarted = false;
        }


        public override Encoding Encoding
        {
            get { return Encoding.Default; }
        }
    }
}
