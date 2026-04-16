// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-11 03:22
// ***********************************************************************
//  <copyright file="GdprStorageState.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

namespace RzR.DataVigil.Abstractions.Enums
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     GDPR processing state of the stored audit entry data.
    /// </summary>
    /// =================================================================================================
    public enum GdprStorageState
    {
        /// <summary>
        ///     Data is stored unmodified.
        /// </summary>
        Original = 0,

        /// <summary>
        ///     Some fields have been processed (masking/hashing/etc).
        /// </summary>
        PartiallyProcessed = 1,

        /// <summary>
        ///     All sensitive data has been fully anonymized.
        /// </summary>
        FullyAnonymized = 2,

        /// <summary>
        ///     Data has been erased per right-to-erasure.
        /// </summary>
        Erased = 3
    }
}