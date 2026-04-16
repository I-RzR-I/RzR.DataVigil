using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RzR.DataVigil.Core.Extensions;
using RzR.DataVigil.Storage.File.Extensions;
using SampleWorkerService.Models;
using SampleWorkerService.Resolvers;
using SampleWorkerService.Workers;

namespace SampleWorkerService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    // Register the audit trail (Core only — no ASP.NET Core)
                    services.AddAuditTrail(options =>
                    {
                        // Identify this application in audit logs
                        options.UseSourceResolver<WorkerSourceResolver>();

                        // File-based storage (one JSON file per day)
                        options.Storage
                            .UseFile(Path.Combine(Directory.GetCurrentDirectory(), "audit-logs"))
                            .WithRetention(30);

                        // GDPR: mask sensitive fields before they hit the store
                        options.Gdpr.ForEntity<Order>(e =>
                        {
                            e.MaskOnStorage(o => o.CustomerEmail);
                            e.MaskOnStorage(o => o.CustomerPhone);

                            // Retrieval: only "Admin" role can see unmasked email
                            e.MaskOnRetrieval(o => o.CustomerEmail, a => a
                                .AllowRoles("Admin"));

                            // Retrieval: only gdpr=full claim can see unmasked phone
                            e.AnonymizeOnRetrieval(o => o.CustomerPhone, a => a
                                .AllowClaim("gdpr", "full"));
                        });
                    });

                    // Register file storage as IAuditStore
                    services.AddAuditTrailFileStorage();

                    // Optional: auto-purge old audit entries every 24 h
                    services.AddAuditRetentionService();

                    // Register the background worker
                    services.AddHostedService<OrderProcessingWorker>();
                });
    }
}
