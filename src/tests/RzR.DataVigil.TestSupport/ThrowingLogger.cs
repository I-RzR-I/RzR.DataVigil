#region U S A G E S

using System;
using Microsoft.Extensions.Logging;

#endregion

namespace RzR.DataVigil.TestSupport
{
    public sealed class ThrowingLogger<TCategory> : ILogger<TCategory>
    {
        public const string SinkFailureMessage = "sink down";

        public bool IsEnabled(LogLevel logLevel)
            => throw new InvalidOperationException(SinkFailureMessage);

        public IDisposable BeginScope<TState>(TState state)
            => throw new InvalidOperationException(SinkFailureMessage);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
            => throw new InvalidOperationException(SinkFailureMessage);
    }
}
