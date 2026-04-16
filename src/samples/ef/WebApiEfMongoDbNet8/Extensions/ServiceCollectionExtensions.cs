// ***********************************************************************
//  Assembly         : RzR.DataVigil.WebApiEfMongoDbNet8
//  Author           : RzR
//  Created On       : 2026-04-15 14:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-15 14:04
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
using MongoDB.Driver;
using RzR.DataVigil.AspNetCore.Extensions;
using RzR.DataVigil.Core.Builder;
using RzR.DataVigil.Core.Extensions;
using RzR.DataVigil.EFCore.Extensions;
using RzR.DataVigil.Storage.EfMongoDb.Extensions;
using WebApiEfMongoDbNet8.Data;
using WebApiEfMongoDbNet8.Models;
using WebApiEfMongoDbNet8.Resolvers;

namespace WebApiEfMongoDbNet8.Extensions
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
                        .UseMongoDb(
                            configuration.GetConnectionString("AuditDb"),
                            configuration["DatabaseNames:AuditDb"]);

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

            serviceCollection.AddAuditTrailMongoDb();

            return serviceCollection;
        }

        public static IServiceCollection RegisterBlogContext(this IServiceCollection serviceCollection, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("BlogDb");
            var databaseName = configuration["DatabaseNames:BlogDb"];

            serviceCollection.AddDbContext<BlogDbContext>((sp, opts) =>
            {
                opts.UseMongoDB(connectionString, databaseName);

                opts.AddAuditInterceptors(sp);
                opts.AddAuditReadInterceptor(sp);
            });

            return serviceCollection;
        }
    }
}
