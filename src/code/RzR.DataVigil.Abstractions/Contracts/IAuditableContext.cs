// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-11 03:22
// ***********************************************************************
//  <copyright file="IAuditableContext.cs" company="RzR SOFT & TECH">
//   Copyright � RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Collections.Generic;

#endregion

namespace RzR.DataVigil.Abstractions.Contracts
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Marks a DbContext/repository as an audit participant. Does not force inheritance, only a
    ///     contract.
    /// </summary>
    /// =================================================================================================
    public interface IAuditableContext
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Entity types excluded globally from audit at the context level.
        /// </summary>
        /// <returns>
        ///     An enumerator that allows foreach to be used to process the excluded entity types in this
        ///     collection.
        /// </returns>
        /// =================================================================================================
        IEnumerable<Type> GetExcludedEntityTypes();
    }
}