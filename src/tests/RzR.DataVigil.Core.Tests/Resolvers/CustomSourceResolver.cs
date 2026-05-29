using System;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.Core.Tests.Resolvers
{
    internal class CustomSourceResolver : IAuditSourceResolver
    {
        public IResult<string> Resolve()
            => throw new NotImplementedException();
    }
}
