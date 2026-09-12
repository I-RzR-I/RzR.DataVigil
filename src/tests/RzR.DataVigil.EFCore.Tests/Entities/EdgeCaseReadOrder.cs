using System;
using System.Collections.Generic;
using RzR.DataVigil.Abstractions.Contracts;

namespace RzR.DataVigil.EFCore.Tests.Entities
{
    public class EdgeCaseReadOrder : IAuditable
    {
        public Guid Id { get; set; }

        public string CustomerName { get; set; }

        public List<string> Tags { get; set; }

        public Dictionary<string, string> Labels { get; set; }
    }
}
