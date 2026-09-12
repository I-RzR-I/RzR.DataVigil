using System;

namespace RzR.DataVigil.EFCore.Tests.Helpers
{
    public class ComplexValueExploding
    {
        public string Boom => throw new InvalidOperationException("Serialization failure.");

        public override string ToString()
        {
            return "exploding-value";
        }
    }
}
