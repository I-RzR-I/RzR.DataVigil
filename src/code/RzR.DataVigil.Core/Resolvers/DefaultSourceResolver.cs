// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 20:12
// ***********************************************************************
//  <copyright file="DefaultSourceResolver.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

#endregion

namespace RzR.DataVigil.Core.Resolvers
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Default source resolver — returns "Unknown". Override with a custom implementation for
    ///     specific host types.
    /// </summary>
    /// <seealso cref="T:RzR.DataVigil.Abstractions.Services.IAuditSourceResolver"/>
    /// =================================================================================================
    public class DefaultSourceResolver : IAuditSourceResolver
    {
        /// <inheritdoc/>
        public IResult<string> Resolve()
        {
            return Result<string>.Success("Unknown");
        }
    }
}