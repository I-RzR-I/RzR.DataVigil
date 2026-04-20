### **v1.0.0.0** [[RzR](mailto:108324929+I-RzR-I@users.noreply.github.com)] 20-04-2026
#### Packages
- `RzR.DataVigil.Abstractions` — contracts, enums, and shared models
- `RzR.DataVigil.Core` — audit pipeline, GDPR processor, options, default resolvers
- `RzR.DataVigil.EFCore` — EF Core SaveChanges and command interceptors
- `RzR.DataVigil.AspNetCore` — HttpContext-based user and correlation resolvers
- `RzR.DataVigil.Storage.EfSqlServer` — SQL Server audit store with EF Core migrations
- `RzR.DataVigil.Storage.EfPostgreSql` — PostgreSQL audit store with EF Core migrations
- `RzR.DataVigil.Storage.EfMongoDb` — MongoDB audit store (embedded document model)
- `RzR.DataVigil.Storage.File` — JSON file-based audit store (one file per day)