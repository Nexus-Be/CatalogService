# 06 — DATABASE PERSISTENCE & MIGRATIONS (CATALOG SERVICE)

## 1. Database Isolation
* Dedicated database: `category_db` on host port `5434`.
* Primary key: 100% `Guid` for entities (`Products`, `Categories`, `ProductVariants`).
* Monetary values (`Price`): explicit precision `HasPrecision(18, 2)`.

## 2. Entity Configuration & Soft Delete
* Every entity declares `IsDeleted` and `DeletedAt`. Enforce query filter: `builder.HasQueryFilter(x => !x.IsDeleted)`.
* Foreign key relationships configured with `DeleteBehavior.Restrict` to prevent accidental cascades.
* Seed Data: Fixed deterministic GUIDs (`Guid.Parse(...)`).

## 3. Migration Safety
* No destructive migrations (`DROP TABLE`, `DROP COLUMN`) without explicit authorization.
* Startup safe auto-migration: `db.Database.Migrate()` wrapped in `try-catch` checking `db.Database.IsRelational()`.
