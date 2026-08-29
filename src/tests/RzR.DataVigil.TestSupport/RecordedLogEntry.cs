#region U S A G E S

using System;
using Microsoft.Extensions.Logging;

#endregion

namespace RzR.DataVigil.TestSupport
{
    public sealed class RecordedLogEntry
    {
        public RecordedLogEntry(LogLevel level, string message, Exception exception)
        {
            Level = level;
            Message = message;
            Exception = exception;
        }

        public LogLevel Level { get; }

        public string Message { get; }

        public Exception Exception { get; }
    }
}
