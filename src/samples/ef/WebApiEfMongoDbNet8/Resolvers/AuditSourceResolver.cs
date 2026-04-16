// ***********************************************************************
//  Assembly         : RzR.DataVigil.WebApiEfMongoDbNet8
//  Author           : RzR
//  Created On       : 2026-04-15 14:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-15 14:04
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

namespace WebApiEfMongoDbNet8.Resolvers
{
    public class AuditSourceResolver : IAuditSourceResolver
    {
        /// <inheritdoc />
        public IResult<string> Resolve()
        {
            return Result<string>.Success("WebApiMongoDb");
        }
    }
}
