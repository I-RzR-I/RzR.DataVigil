// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-14 19:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 19:41
// ***********************************************************************
//  <copyright file="StorageOptions.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

namespace RzR.DataVigil.Core.Options
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Configuration options for audit storage.
    /// </summary>
    /// =================================================================================================
    public sealed class StorageOptions
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Database schema for audit tables. Default: "audit".
        /// </summary>
        /// <value>
        ///     The schema.
        /// </value>
        /// =================================================================================================
        public string Schema { get; set; } = "audit";

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Connection string for database-backed stores.
        /// </summary>
        /// <value>
        ///     The connection string.
        /// </value>
        /// =================================================================================================
        public string ConnectionString { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Database name for document-oriented stores (e.g. MongoDB).
        /// </summary>
        /// <value>
        ///     The database name.
        /// </value>
        /// =================================================================================================
        public string DatabaseName { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     File path for file-based store.
        /// </summary>
        /// <value>
        ///     The full pathname of the file.
        /// </value>
        /// =================================================================================================
        public string FilePath { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Retention period in days. Null = no auto-purge.
        /// </summary>
        /// <value>
        ///     The retention days.
        /// </value>
        /// =================================================================================================
        public int? RetentionDays { get; private set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Set the retention policy (auto-delete entries older than N days).
        /// </summary>
        /// <param name="days">The days.</param>
        /// <returns>
        ///     The StorageOptions.
        /// </returns>
        /// =================================================================================================
        public StorageOptions WithRetention(int days)
        {
            RetentionDays = days;

            return this;
        }
    }
}