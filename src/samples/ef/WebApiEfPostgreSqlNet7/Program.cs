using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using RzR.DataVigil.Storage.EfPostgreSql.Extensions;
using WebApiEfPostgreSqlNet7.Data;
using WebApiEfPostgreSqlNet7.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.RegisterBlogContext(builder.Configuration);
builder.Services.RegisterEntityAuditTrail(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "WebApiEfPostgreSqlNet7", Version = "v1" });
});

var app = builder.Build();

// Apply audit migrations
app.Services.MigrateAuditPostgreSqlDb();

using (var scope = app.Services.CreateScope())
{
    var blogDb = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
    blogDb.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "WebApiEfPostgreSqlNet7 v1"));
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();
