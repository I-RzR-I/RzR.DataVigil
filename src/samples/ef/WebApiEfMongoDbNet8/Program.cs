using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using RzR.DataVigil.AspNetCore.Extensions;
using WebApiEfMongoDbNet8.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.RegisterBlogContext(builder.Configuration);
builder.Services.RegisterEntityAuditTrail(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "WebApiEfMongoDbNet8", Version = "v1" });
});

var app = builder.Build();

// MongoDB is schema-less — no migrations needed.
// Collections are created automatically when data is first inserted.

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "WebApiEfMongoDbNet8 v1"));
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.UseAuditReadFlush();
app.MapControllers();

app.Run();
