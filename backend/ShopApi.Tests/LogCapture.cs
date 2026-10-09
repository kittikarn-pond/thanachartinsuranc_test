using Microsoft.Extensions.Logging;

namespace ShopApi.Tests;

public record LogEntry(LogLevel Level, string Message, Exception? Exception);

/// <summary>Collects everything logged through the factories it creates.</summary>
public class LogCapture : ILoggerProvider
{
    public List<LogEntry> Entries { get; } = [];

    public ILoggerFactory Factory => LoggerFactory.Create(b => b.AddProvider(this));

    public ILogger CreateLogger(string categoryName) => new Logger(Entries);

    public void Dispose() { }

    private class Logger(List<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }
}
