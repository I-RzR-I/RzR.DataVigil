using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using RzR.DataVigil.Storage.EfPostgreSql.Extensions;
using WebApiEfPostgreSqlNet5.Data;
using WebApiEfPostgreSqlNet5.Extensions;

namespace WebApiEfPostgreSqlNet5
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.RegisterBlogContext(Configuration);
            services.RegisterEntityAuditTrail(Configuration);

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "WebApiEfPostgreSqlNet5", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Apply audit migrations (creates audit schema + tables)
            app.ApplicationServices.MigrateAuditPostgreSqlDb();

            using (var scope = app.ApplicationServices.CreateScope())
            {
                // Create blog schema and tables if they don't exist
                var blogDb = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
                //blogDb.Database.EnsureCreated();
                blogDb.Database.Migrate();
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "WebApiEfPostgreSqlNet5 v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
