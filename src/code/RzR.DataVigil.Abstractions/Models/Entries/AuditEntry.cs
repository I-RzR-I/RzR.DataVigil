// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 00:00
// ***********************************************************************
//  <copyright file="AuditEntry.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Collections.Generic;
using RzR.DataVigil.Abstractions.Enums;

#endregion

namespace RzR.DataVigil.Abstractions.Models.Entries
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Represents a single entity change within an audit transaction.
    /// </summary>
    /// =================================================================================================
    public class AuditEntry
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Unique identifier of the audit entry.
        /// </summary>
        /// <value>
        ///     The identifier.
        /// </value>
        /// =================================================================================================
        public Guid Id { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Foreign key to the parent audit transaction.
        /// </summary>
        /// <value>
        ///     The identifier of the transaction.
        /// </value>
        /// =================================================================================================
        public Guid TransactionId { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     The type of action that was performed (Create, Read, Update, Delete).
        /// </summary>
        /// <value>
        ///     The action.
        /// </value>
        /// =================================================================================================
        public AuditAction Action { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Fully qualified name of the audited entity (e.g. "Order", "Customer").
        /// </summary>
        /// <value>
        ///     The name of the entity.
        /// </value>
        /// =================================================================================================
        public string EntityName { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Primary key of the audited entity, serialized as string.
        /// </summary>
        /// <value>
        ///     The identifier of the entity.
        /// </value>
        /// =================================================================================================
        public string EntityId { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     CLR type name of the audited entity (e.g. "MyApp.Domain.Order").
        /// </summary>
        /// <value>
        ///     The entity type name.
        /// </value>
        /// =================================================================================================
        public string EntityTypeName { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Individual property changes. Enables per-field diff, not just a JSON blob.
        /// </summary>
        /// <value>
        ///     The properties.
        /// </value>
        /// =================================================================================================
        public ICollection<AuditEntryProperty> Properties { get; set; } = new List<AuditEntryProperty>();
    }
}