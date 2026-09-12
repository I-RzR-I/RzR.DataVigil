using System;
using System.Collections.Generic;

namespace RzR.DataVigil.EFCore.Tests.Entities
{
    public class ComplexValueOrder
    {
        public Guid Id { get; set; }

        public string CustomerName { get; set; }

        public ComplexValueOrderStatus Status { get; set; }

        public List<string> Tags { get; set; }

        public byte[] RowVersion { get; set; }
    }
}
