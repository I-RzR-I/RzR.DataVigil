// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-11 03:22
// ***********************************************************************
//  <copyright file="GdprFieldAction.cs" company="RzR SOFT & TECH">
//   Copyright � RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

namespace RzR.DataVigil.Abstractions.Enums
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     GDPR action applied to a specific field.
    /// </summary>
    /// =================================================================================================
    public enum GdprFieldAction
    {
        /// <summary>
        ///     Field is not stored/displayed at all.
        /// </summary>
        Exclude = 0,

        /// <summary>
        ///     Partially hidden (e.g. j***@mail.com).
        /// </summary>
        Mask = 1,

        /// <summary>
        ///     Fully replaced with [ANONYMIZED].
        /// </summary>
        Anonymize = 2,

        /// <summary>
        ///     SHA-256 hash for pseudonymization.
        /// </summary>
        Hash = 3,

        /// <summary>
        ///     Custom transformation via delegate.
        /// </summary>
        Custom = 4
    }
}