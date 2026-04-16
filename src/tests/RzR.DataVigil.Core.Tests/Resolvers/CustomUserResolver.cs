using System;
using AggregatedGenericResultMessage.Abstractions;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;

namespace RzR.DataVigil.Core.Tests.Resolvers
{
    internal class CustomUserResolver : IAuditUserResolver
    {
        public IResult<AuditUserInfo> Resolve()
            => throw new NotImplementedException();
    }
}
