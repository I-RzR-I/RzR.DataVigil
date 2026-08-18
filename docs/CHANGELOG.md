### **v1.2.0.809** [[RzR](mailto:108324929+I-RzR-I@users.noreply.github.com)] 19-08-2026
* [55076dc] (RzR) -> Auto commit uncommited files
* [ae58611] (RzR) -> Document the audit identity fixes and their known limitations.
* [badbcc9] (RzR) -> Add audit user source and reserved metadata key.
* [2bf9e63] (RzR) -> Make resolver registration independent of call order.
* [49ffab9] (RzR) -> Persist EF Core audit records after the write completes.
* [4c2c954] (RzR) -> Stop applying GDPR storage policies twice in the file store.
* [88c13f2] (RzR) -> Fix audit identity resolution and record attribution provenance.
* [fa0bc0a] (RzR) -> Add audit user source and reserved metadata key.

### **v1.1.0.8085** [[RzR](mailto:108324929+I-RzR-I@users.noreply.github.com)] 29-05-2026
* [7dab209] (RzR) -> Auto commit uncommited files
* [142509b] (RzR) -> Add benchmarks project and upfate reference packages.

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
