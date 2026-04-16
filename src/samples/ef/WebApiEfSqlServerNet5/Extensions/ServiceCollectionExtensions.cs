// ***********************************************************************
//  Assembly         : RzR.DataVigil.WebApiEfSqlServerNet5
//  Author           : RzR
//  Created On       : 2026-04-15 17:30
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-15 17:30
// ***********************************************************************
//  <copyright file="ServiceCollectionExtensions.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RzR.DataVigil.AspNetCore.Extensions;
using RzR.DataVigil.Core.Builder;
using RzR.DataVigil.Core.Extensions;
using RzR.DataVigil.Core.Resolvers;
using RzR.DataVigil.EFCore.Extensions;
using RzR.DataVigil.Storage.EfSqlServer;
using RzR.DataVigil.Storage.EfSqlServer.Extensions;
using System.Reflection;
using WebApiEfSqlServerNet5.Data;
using WebApiEfSqlServerNet5.Models;
using WebApiEfSqlServerNet5.Resolvers;

namespace WebApiEfSqlServerNet5.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection RegisterEntityAuditTrail(this IServiceCollection serviceCollection, IConfiguration configuration)
        {
            serviceCollection.AddAuditTrail(options =>
                {
                    options.UseSourceResolver<AuditSourceResolver>();

                    options.EfCore
                        .Intercept<BlogDbContext>()
                        .IncludeReads()
                        .IncludeReadProperties();

                    options.Storage
                            .UseSqlServer(configuration.GetConnectionString("AuditDb"))
                            .WithRetention(90);

                    options.Storage.Schema = "audit";

                    options.Gdpr.ForEntity<Post>(e =>
                    {
                        e.MaskOnStorage(x => x.Title);
                        e.AnonymizeOnStorage(x => x.Author);
                        e.ExcludeOnStorage(x => x.Body);
                        e.HashOnStorage(x => x.CreatedAt);
                        e.TransformOnStorage(x => x.UpdatedAt, s => $"#2{s}");
                        
                        e.MaskOnRetrieval(x => x.Title);
                        e.AnonymizeOnRetrieval(x => x.Author);
                    });
                })
                .Services
                .AddAuditTrailEfCore()
                .AddAuditTrailAspNetCore();

            serviceCollection.AddAuditTrailSqlServer();

            return serviceCollection;
        }

        public static IServiceCollection RegisterBlogContext(this IServiceCollection serviceCollection, IConfiguration configuration)
        {
            var connect = configuration.GetConnectionString("BlogDb");

            serviceCollection.AddDbContext<BlogDbContext>((sp, opts) =>
            {
                opts.UseSqlServer(connect, options =>
                {
                    options.MigrationsAssembly(Assembly.GetExecutingAssembly().GetName().Name);
                    options.MigrationsHistoryTable($"__{nameof(BlogDbContext)}", "blog");
                });

                opts.AddAuditInterceptors(sp);
            });

            return serviceCollection;
        }
    }
}
