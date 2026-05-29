// ***********************************************************************
//  Assembly          : RzR.DataVigil.RzR.DataVigil.Benchmarks
//  Author            : RzR
//  Created           : 20-04-2026 13:04
// 
//  Last Modified By : RzR
//  Last Modified On : 20-05-2026 23:08
//  ***********************************************************************
//  <copyright file="StubSourceResolver.cs" company="RzR SOFT & TECH">
//      Copyright (c) RzR. All rights reserved.
//  </copyright>
//  <contact>
//      https://iamrzr.dev/contact
//  </contact>
//  <summary></summary>
//  ***********************************************************************

#region U S I N G

using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

#endregion

namespace RzR.DataVigil.Benchmarks.Resolvers
{
    public sealed class StubSourceResolver : IAuditSourceResolver
    {
        public IResult<string> Resolve()
        {
            return Result<string>.Success("BenchmarkRunner");
        }
    }
}