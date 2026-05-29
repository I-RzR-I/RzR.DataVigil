using System;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.Core.Tests.Resolvers
{
    internal class CustomUserResolver : IAuditUserResolver
    {
        public IResult<AuditUserInfo> Resolve()
            => throw new NotImplementedException();
    }
}
