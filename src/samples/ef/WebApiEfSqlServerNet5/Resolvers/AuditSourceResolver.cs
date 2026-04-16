// ***********************************************************************
//  Assembly         : RzR.DataVigil.WebApiEfSqlServerNet5
//  Author           : RzR
//  Created On       : 2026-04-15 17:30
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-15 17:30
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

namespace WebApiEfSqlServerNet5.Resolvers
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
