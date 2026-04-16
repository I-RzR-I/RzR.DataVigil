using System;
using AggregatedGenericResultMessage.Abstractions;
using RzR.DataVigil.Abstractions.Services;

namespace RzR.DataVigil.Core.Tests.Resolvers
{
    internal class CustomSourceResolver : IAuditSourceResolver
    {
        public IResult<string> Resolve()
            => throw new NotImplementedException();
    }
}
