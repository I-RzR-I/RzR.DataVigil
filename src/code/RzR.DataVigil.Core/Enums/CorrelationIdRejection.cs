// ***********************************************************************
//  Assembly          : RzR.DataVigil.RzR.DataVigil.Core
//  Author            : RzR
//  Created           : 2026-08-29 00:15
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-08-29 00:15
//  ***********************************************************************
//  <copyright file="CorrelationIdRejection.cs" company="RzR SOFT & TECH">
//      Copyright (c) RzR. All rights reserved.
//  </copyright>
//  <contact>
//      https://iamrzr.dev/contact
//  </contact>
//  <summary></summary>
//  ***********************************************************************

#region U S A G E S

using RzR.DataVigil.Core.Helpers;

#endregion

namespace RzR.DataVigil.Core.Enums
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Why <see cref="CorrelationIdValidator"/> refused a candidate correlation id.
    /// </summary>
    /// =================================================================================================
    internal enum CorrelationIdRejection
    {
        /// <summary>The candidate is an acceptable correlation id.</summary>
        Accepted = 0,

        /// <summary>No candidate was supplied, or it was empty once trimmed.</summary>
        Missing,

        /// <summary>The candidate is longer than the storage column allows.</summary>
        TooLong,

        /// <summary>The candidate carries a character that is unsafe to persist.</summary>
        DisallowedCharacter
    }
}