// ***********************************************************************
//  Assembly         : RzR.DataVigil.WebApiEfPostgreSqlNet5
//  Author           : RzR
//  Created On       : 2026-04-14 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-15 11:17
// ***********************************************************************
//  <copyright file="AuditSourceResolver.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using AggregatedGenericResultMessage;
using AggregatedGenericResultMessage.Abstractions;
using RzR.DataVigil.Abstractions.Services;

#endregion

namespace WebApiEfPostgreSqlNet5.Resolvers
{
    public class AuditSourceResolver : IAuditSourceResolver
    {
        /// <inheritdoc />
        public IResult<string> Resolve()
        {
            return Result<string>.Success("WebApiNet5");
        }
    }
}