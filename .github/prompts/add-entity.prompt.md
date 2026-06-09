---
mode: agent
description: Add a new EF Core entity with migration
---

Add a new EF Core entity to DocQA.Server.

Steps:
1. Create the model class in `DocQA.Server/Models/`
2. Add a `DbSet<T>` property to `AppDbContext`
3. Configure any relationships or constraints in `OnModelCreating` if needed
4. Add the corresponding DTO(s) to `DocQA.Shared/`
5. Provide the migration command to run: `dotnet ef migrations add <MigrationName> --project DocQA.Server`

Entity to add: [DESCRIBE THE ENTITY AND ITS FIELDS HERE]
