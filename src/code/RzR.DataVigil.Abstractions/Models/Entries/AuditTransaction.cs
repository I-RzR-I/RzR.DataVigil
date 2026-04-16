// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-14 00:00
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 00:00
// ***********************************************************************
//  <copyright file="AuditTransaction.cs" company="RzR SOFT & TECH">
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
    ///     Represents a single audit transaction (event envelope) that groups all entity
    ///     changes from a single SaveChanges/command call. Contains actor, tracing, and
    ///     GDPR information shared across all entries in the transaction.
    /// </summary>
    /// =================================================================================================
    public class AuditTransaction
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Unique identifier of the audit transaction.
        /// </summary>
        /// <value>
        ///     The identifier.
        /// </value>
        /// =================================================================================================
        public Guid Id { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     UTC timestamp when the audited action occurred.
        /// </summary>
        /// <value>
        ///     The timestamp.
        /// </value>
        /// =================================================================================================
        public DateTimeOffset Timestamp { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Identifier of the user who performed the action.
        /// </summary>
        /// <value>
        ///     The identifier of the user.
        /// </value>
        /// =================================================================================================
        public string UserId { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Display name of the user who performed the action.
        /// </summary>
        /// <value>
        ///     The name of the user.
        /// </value>
        /// =================================================================================================
        public string UserName { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     IP address of the client that triggered the audited action.
        /// </summary>
        /// <value>
        ///     The IP address.
        /// </value>
        /// =================================================================================================
        public string IpAddress { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Correlation identifier used to group related audit entries across services.
        /// </summary>
        /// <value>
        ///     The identifier of the correlation.
        /// </value>
        /// =================================================================================================
        public string CorrelationId { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Distributed trace identifier for end-to-end request tracking.
        /// </summary>
        /// <value>
        ///     The identifier of the trace.
        /// </value>
        /// =================================================================================================
        public string TraceId { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Origin of the audited action (e.g. "WebApi", "WorkerService", "Console").
        /// </summary>
        /// <value>
        ///     The source.
        /// </value>
        /// =================================================================================================
        public string Source { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Current GDPR processing state of the stored data.
        /// </summary>
        /// <value>
        ///     The gdpr state.
        /// </value>
        /// =================================================================================================
        public GdprStorageState GdprState { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Arbitrary key-value metadata attached to the audit transaction. Serialized as JSON in storage.
        /// </summary>
        /// <value>
        ///     The metadata.
        /// </value>
        /// =================================================================================================
        public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Entity-level audit entries belonging to this transaction.
        /// </summary>
        /// <value>
        ///     The entries.
        /// </value>
        /// =================================================================================================
        public ICollection<AuditEntry> Entries { get; set; } = new List<AuditEntry>();
    }
}
