// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-11 03:22
// ***********************************************************************
//  <copyright file="AuditEntryProperty.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

namespace RzR.DataVigil.Abstractions.Models.Entries
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Old/new value of an individual property. Enables per-field visual diff and per-field GDPR
    ///     processing.
    /// </summary>
    /// =================================================================================================
    public class AuditEntryProperty
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Name of the entity property that was changed.
        /// </summary>
        /// <value>
        ///     The name of the property.
        /// </value>
        /// =================================================================================================
        public string PropertyName { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     .NET type of the property (e.g. "System.String", "System.Int32"). Useful for correct
        ///     deserialization in UI.
        /// </summary>
        /// <value>
        ///     The type of the property.
        /// </value>
        /// =================================================================================================
        public string PropertyType { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Serialized value before modification. Null for Create.
        /// </summary>
        /// <value>
        ///     The old value.
        /// </value>
        /// =================================================================================================
        public string OldValue { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Serialized value after modification. Null for Delete.
        /// </summary>
        /// <value>
        ///     The new value.
        /// </value>
        /// =================================================================================================
        public string NewValue { get; set; }
    }
}