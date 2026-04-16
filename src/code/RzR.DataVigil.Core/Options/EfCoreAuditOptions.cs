// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-14 19:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 19:40
// ***********************************************************************
//  <copyright file="EfCoreAuditOptions.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Collections.Generic;

#endregion

namespace RzR.DataVigil.Core.Options
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Configuration options for EF Core audit interception.
    /// </summary>
    /// =================================================================================================
    public sealed class EfCoreAuditOptions
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     DbContext types to intercept.
        /// </summary>
        /// <value>
        ///     A list of types of the contexts.
        /// </value>
        /// =================================================================================================
        internal IList<Type> ContextTypes { get; } = new List<Type>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Whether to audit Read operations (SELECT queries).
        /// </summary>
        /// <value>
        ///     True if include reads enabled, false if not.
        /// </value>
        /// =================================================================================================
        public bool IncludeReadsEnabled { get; private set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Whether to include property (column) values in Read audit entries. Only has effect when
        ///     IncludeReads is also enabled.
        /// </summary>
        /// <value>
        ///     True if include read properties enabled, false if not.
        /// </value>
        /// =================================================================================================
        public bool IncludeReadPropertiesEnabled { get; private set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets a value indicating whether the include read properties value is enabled.
        /// </summary>
        /// <value>
        ///     True if include read properties value enabled, false if not.
        /// </value>
        /// =================================================================================================
        public bool IncludeReadPropertiesValueEnabled { get; private set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Register a DbContext type for audit interception.
        /// </summary>
        /// <typeparam name="TContext">Type of the context.</typeparam>
        /// <returns>
        ///     The EfCoreAuditOptions.
        /// </returns>
        /// =================================================================================================
        public EfCoreAuditOptions Intercept<TContext>() where TContext : class
        {
            ContextTypes.Add(typeof(TContext));

            return this;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Enable auditing of Read (SELECT) operations.
        /// </summary>
        /// <returns>
        ///     The EfCoreAuditOptions.
        /// </returns>
        /// =================================================================================================
        public EfCoreAuditOptions IncludeReads()
        {
            IncludeReadsEnabled = true;

            return this;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Enable logging property (column) values in Read audit entries. Requires IncludeReads() to
        ///     be enabled.
        /// </summary>
        /// <returns>
        ///     The EfCoreAuditOptions.
        /// </returns>
        /// =================================================================================================
        public EfCoreAuditOptions IncludeReadProperties()
        {
            IncludeReadPropertiesEnabled = true;
            IncludeReadPropertiesValueEnabled = false;

            return this;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Returns true if the given context type should be audited. If no context types are
        ///     registered, all IAuditableContext contexts are audited.
        /// </summary>
        /// <param name="contextType">Type of the context.</param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
        /// =================================================================================================
        public bool ShouldAuditContext(Type contextType)
        {
            return ContextTypes.Count == 0 || ContextTypes.Contains(contextType);
        }
    }
}