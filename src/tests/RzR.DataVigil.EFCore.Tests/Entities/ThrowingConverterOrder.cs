using System;

namespace RzR.DataVigil.EFCore.Tests.Entities
{
    public class ThrowingConverterOrder
    {
        public Guid Id { get; set; }

        public string Payload { get; set; }

        public static string FailingConvert(string value)
        {
            throw new InvalidOperationException("Converter failure for " + value + ".");
        }
    }
}
