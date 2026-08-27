// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-08-27 10:00
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-27 10:00
// ***********************************************************************
//  <copyright file="RefAuditUserSource.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
//
//  <summary>
//  </summary>
// ***********************************************************************

namespace RzR.DataVigil.Abstractions.Models.Entries.Referees
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Referee (lookup) table row describing a single
    ///     <see cref="T:RzR.DataVigil.Abstractions.Enums.AuditUserSource"/> member.
    /// </summary>
    /// =================================================================================================
    public class RefAuditUserSource
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Numeric value of the underlying
        ///     <see cref="T:RzR.DataVigil.Abstractions.Enums.AuditUserSource"/> member.
        /// </summary>
        /// <value>
        ///     The identifier.
        /// </value>
        /// =================================================================================================
        public int Id { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Name of the underlying enum member.
        /// </summary>
        /// <value>
        ///     The name.
        /// </value>
        /// =================================================================================================
        public string Name { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Human-readable description of what the enum member represents.
        /// </summary>
        /// <value>
        ///     The description.
        /// </value>
        /// =================================================================================================
        public string Description { get; set; }
    }
}
