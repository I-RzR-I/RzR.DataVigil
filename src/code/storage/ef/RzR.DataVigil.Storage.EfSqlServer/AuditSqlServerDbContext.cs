// ***********************************************************************
//  Assembly         : RzR.DataVigil.Storage.EfSqlServer
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-15 12:00
// ***********************************************************************
//  <copyright file="AuditSqlServerDbContext.cs" company="RzR SOFT & TECH">
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

namespace RzR.DataVigil.Storage.EfSqlServer
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     SQL Server-specific audit DbContext.
    ///     Inherits table and schema configuration from <see cref="AuditDbContextBase"/>.
    /// </summary>
    /// <seealso cref="T:RzR.DataVigil.EFCore.AuditDbContextBase"/>
    /// =================================================================================================
    public class AuditSqlServerDbContext : AuditDbContextBase
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AuditSqlServerDbContext"/> class.
        /// </summary>
        /// <param name="options">The SQL Server DbContext options.</param>
        /// <param name="storageOptions">Storage configuration containing the schema name.</param>
        /// =================================================================================================
        public AuditSqlServerDbContext(
            DbContextOptions<AuditSqlServerDbContext> options,
            StorageOptions storageOptions)
            : base(options, storageOptions.Schema)
        {
        }
    }
}
