// ***********************************************************************
//  Assembly         : RzR.DataVigil.Storage.EfPostgreSql
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-15 12:00
// ***********************************************************************
//  <copyright file="AuditPostgreSqlDbContext.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using Microsoft.EntityFrameworkCore;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.EFCore;

#endregion

namespace RzR.DataVigil.Storage.EfPostgreSql
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     PostgreSQL-specific audit DbContext.
    ///     Inherits table and schema configuration from <see cref="AuditDbContextBase"/>.
    /// </summary>
    /// <seealso cref="T:RzR.DataVigil.EFCore.AuditDbContextBase"/>
    /// =================================================================================================
    public class AuditPostgreSqlDbContext : AuditDbContextBase
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AuditPostgreSqlDbContext"/> class.
        /// </summary>
        /// <param name="options">The PostgreSQL DbContext options.</param>
        /// <param name="storageOptions">Storage configuration containing the schema name.</param>
        /// =================================================================================================
        public AuditPostgreSqlDbContext(
            DbContextOptions<AuditPostgreSqlDbContext> options,
            StorageOptions storageOptions)
            : base(options, storageOptions.Schema)
        {
        }
    }
}
