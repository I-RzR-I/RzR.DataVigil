#region U S A G E S

using System;

#endregion

namespace RzR.DataVigil.TestSupport
{
    internal sealed class NullScope : IDisposable
    {
        internal static readonly NullScope Instance = new NullScope();

        public void Dispose()
        {
        }
    }
}
