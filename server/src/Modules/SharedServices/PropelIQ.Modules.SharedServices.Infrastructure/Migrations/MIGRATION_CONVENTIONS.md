# Migration Conventions (DR-007)

## Rules

1. **Never modify an existing migration** that has been applied to any environment.
   Always generate a new migration for changes.

2. **Additive only.** New changes always use `dotnet ef migrations add <DescriptiveName>`.

3. **Destructive changes require a two-migration strategy:**
   - Migration 1: Make the column nullable or add the new column as nullable.
   - Migration 2 (next sprint): Remove the old column or apply the NOT NULL constraint
     after data has been migrated.

4. **Pre-migration constraint guards are mandatory** when adding a unique or NOT NULL
   constraint to an existing populated table. Use a PL/pgSQL DO block to check for
   violations before applying the constraint:

   ```csharp
   migrationBuilder.Sql(@"
       DO $$
       BEGIN
           IF EXISTS (
               SELECT column_name, COUNT(*)
               FROM app.table_name
               GROUP BY column_name
               HAVING COUNT(*) > 1
           ) THEN
               RAISE EXCEPTION
                   'Migration blocked: duplicate values exist in column_name. '
                   'Run data fix script before applying migration.';
           END IF;
       END $$;
   ");
   ```

5. **Migration naming:** Use PascalCase descriptive names (e.g., `AddPatientPhoneColumn`,
   `CreateAuditIndexes`). Avoid generic names like `Update1`.

## Commands

```bash
# Generate a new migration
dotnet ef migrations add <Name> \
  --project src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure \
  --startup-project src/PropelIQ.Api

# Apply all pending migrations
dotnet ef database update \
  --project src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure \
  --startup-project src/PropelIQ.Api

# Rollback to a specific migration
dotnet ef database update <MigrationName> \
  --project src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure \
  --startup-project src/PropelIQ.Api

# Check for pending model changes
dotnet ef migrations has-pending-model-changes \
  --project src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure \
  --startup-project src/PropelIQ.Api
```
