// ***********************************************************************
//  Assembly          : RzR.DataVigil.RzR.DataVigil.Benchmarks
//  Author            : RzR
//  Created           : 20-04-2026 13:04
// 
//  Last Modified By : RzR
//  Last Modified On : 20-05-2026 23:08
//  ***********************************************************************
//  <copyright file="StubCorrelationProvider.cs" company="RzR SOFT & TECH">
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

namespace RzR.DataVigil.Benchmarks.Providers
{
    public sealed class StubCorrelationProvider : IAuditCorrelationProvider
    {
        public IResult<string> GetCorrelationId()
        {
            return Result<string>.Success("corr-bench-001");
        }

        public IResult<string> GetTraceId()
        {
            return Result<string>.Success("trace-bench-001");
        }
    }
}