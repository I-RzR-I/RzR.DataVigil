using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace RzR.DataVigil.AspNetCore.Tests.Stubs
{
    internal sealed class RecordingLogger<T> : ILogger<T>
    {
        internal List<(LogLevel Level, string Message)> Entries { get; } =
            new List<(LogLevel Level, string Message)>();

        internal LogLevel MinLevel { get; set; } = LogLevel.Trace;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= MinLevel;

        public IDisposable BeginScope<TState>(TState state) => null;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    internal sealed class ThrowingLogger<T> : ILogger<T>
    {
        internal const string SinkFailureMessage = "sink down";

        public bool IsEnabled(LogLevel logLevel)
            => throw new InvalidOperationException(SinkFailureMessage);

        public IDisposable BeginScope<TState>(TState state)
            => throw new InvalidOperationException(SinkFailureMessage);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
            => throw new InvalidOperationException(SinkFailureMessage);
    }
}
