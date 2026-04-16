// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-11 03:22
// ***********************************************************************
//  <copyright file="AuditAction.cs" company="RzR SOFT & TECH">
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
    ///     Audit action type.
    /// </summary>
    /// =================================================================================================
    public enum AuditAction
    {
        /// <summary>
        ///     An enum constant representing the create option.
        /// </summary>
        Create = 1,

        /// <summary>
        ///     An enum constant representing the read option.
        /// </summary>
        Read = 2,

        /// <summary>
        ///     An enum constant representing the update option.
        /// </summary>
        Update = 3,

        /// <summary>
        ///     An enum constant representing the delete option.
        /// </summary>
        Delete = 4
    }
}