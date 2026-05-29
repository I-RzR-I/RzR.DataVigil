// ***********************************************************************
//  Assembly          : RzR.DataVigil.RzR.DataVigil.Benchmarks
//  Author            : RzR
//  Created           : 20-04-2026 13:04
// 
//  Last Modified By : RzR
//  Last Modified On : 20-05-2026 23:08
//  ***********************************************************************
//  <copyright file="StubUserResolver.cs" company="RzR SOFT & TECH">
//      Copyright (c) RzR. All rights reserved.
//  </copyright>
//  <contact>
//      https://iamrzr.dev/contact
//  </contact>
//  <summary></summary>
//  ***********************************************************************

#region U S I N G

using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

#endregion

namespace RzR.DataVigil.Benchmarks.Resolvers
{
    public sealed class StubUserResolver : IAuditUserResolver
    {
        public IResult<AuditUserInfo> Resolve()
        {
            return Result<AuditUserInfo>.Success(new AuditUserInfo
            {
                UserId = "bench-user",
                UserName = "Benchmark",
                IpAddress = "127.0.0.1"
            });
        }
    }
}