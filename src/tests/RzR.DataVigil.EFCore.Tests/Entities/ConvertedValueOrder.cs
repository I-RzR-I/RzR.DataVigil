using System;
using System.Collections.Generic;

namespace RzR.DataVigil.EFCore.Tests.Entities
{
    public class ConvertedValueOrder
    {
        public Guid Id { get; set; }

        public string CustomerName { get; set; }

        public Dictionary<string, string> NameI18n { get; set; }

        public Guid SecretId { get; set; }

        public string NonDeterministicTag { get; set; }
    }
}
