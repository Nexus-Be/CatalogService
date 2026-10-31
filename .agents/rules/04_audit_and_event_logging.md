# 04 — AUDIT LOGGING & CATALOG EVENTS (CATALOG SERVICE)

## 1. Mandatory Audit Logging
Record audit logs for critical mutating operations:
* Product creation, product updates, price modifications, stock adjustments.
* Category creation, updates, and deactivation.
* Product deletion or archival.

## 2. Audit Persistence Standards
* Entities inherit `AuditableEntity<Guid>` (`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`).
* Audit rows are written into this service's own `AuditLogs` table through `_dbContext.AddAuditLog(...)` (`Catalog.Application/Common/Extensions/AuditLogExtensions.cs`).
* The audit row is committed in the same database transaction as the business change.
* Never instantiate `new AuditLog { ... }` outside the extension. Never replace an audit row with `_logger.LogInformation`.
* Never call `LogService` over HTTP inside the business transaction. Rows start with `IsSynced = false`; a background sync may later forward them to `POST /api/v1/internal/logs/batch` with `X-Internal-Service-Key`.
* Audit rows are append-only. INSERT and SELECT only, never UPDATE or DELETE.
* Field reference: `ServiceName`, `Action`, `EntityName`, `EntityId`, `UserId`, `UserName`, `OldValues`, `NewValues`, `ChangedColumns`, `TraceId`, `IpAddress`, `UserAgent`, `CreatedAt`, `IsSynced`.
