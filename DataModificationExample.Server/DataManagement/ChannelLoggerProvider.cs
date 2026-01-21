using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace DataModificationExample.Server.DataManagement;

public class ChannelLoggerProvider : ILoggerProvider
{
    private readonly ChannelWriter<DataModificationLogEvent> _channelWriter;
    private readonly LogLevel _minLevel;

    public ChannelLoggerProvider(ChannelWriter<DataModificationLogEvent> channelWriter, LogLevel minLevel = LogLevel.Information)
    {
        _channelWriter = channelWriter;
        _minLevel = minLevel;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new ChannelLogger(categoryName, _channelWriter, _minLevel);
    }

    public void Dispose()
    {
    }

    private class ChannelLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly ChannelWriter<DataModificationLogEvent> _channelWriter;
        private readonly LogLevel _minLevel;

        public ChannelLogger(string categoryName, ChannelWriter<DataModificationLogEvent> channelWriter, LogLevel minLevel)
        {
            _categoryName = categoryName;
            _channelWriter = channelWriter;
            _minLevel = minLevel;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");

            var logEvent = new DataModificationLogMessage(
                timestamp,
                logLevel.ToString(),
                _categoryName,
                message
            );

            _channelWriter.TryWrite(logEvent);

            if (exception != null)
            {
                var exceptionEvent = new DataModificationLogMessage(
                    timestamp,
                    LogLevel.Error.ToString(),
                    _categoryName,
                    $"Exception: {exception}"
                );
                _channelWriter.TryWrite(exceptionEvent);
            }
        }
    }
}
