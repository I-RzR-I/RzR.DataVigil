#region U S A G E S

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

#endregion

namespace RzR.DataVigil.TestSupport
{
    public sealed class RecordingLogger<TCategory> : ILogger<TCategory>
    {
        private readonly List<RecordedLogEntry> _entries = new List<RecordedLogEntry>();

        public IReadOnlyList<RecordedLogEntry> Entries => _entries;

        public LogLevel MinLevel { get; set; } = LogLevel.Trace;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= MinLevel;

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
            => _entries.Add(new RecordedLogEntry(logLevel, formatter(state, exception), exception));
    }
}
