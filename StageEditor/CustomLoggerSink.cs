using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace Haven
{
    public class CustomLoggerSink : ILogEventSink
    {
        public readonly MessageTemplateTextFormatter Formatter;

        public event EventHandler? NewLogHandler;

        public CustomLoggerSink(IFormatProvider formatProvider, string outputTemplate) {
            Formatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
        }

        public void Emit(LogEvent logEvent)
        {
            NewLogHandler?.Invoke(typeof(CustomLoggerSink), new LogEventArgs() { Log = logEvent });
        }
    }

    public class LogEventArgs : EventArgs
    {
        public LogEvent Log { get; set; }
    }

}
