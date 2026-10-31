# 05 — ERROR HANDLING & ENUM BOUNDARY (CATALOG SERVICE)

## 1. 100% English & Sentence Case
* Error messages must be 100% English sentence case.
* Capitalize only the first letter. Keep technical acronyms uppercase (`ID`, `SKU`, `URL`).
* Examples: `"Product not found with the specified ID."`, `"Category not found with the specified ID."`, `"Product name cannot be empty."`

## 2. SCREAMING_SNAKE_CASE Error Codes
* Error codes must be uppercase with underscores. No `ERR_` prefixes.
* Use `VAL_` prefix for validation errors.
* Examples: `PRODUCT_NOT_FOUND`, `CATEGORY_NOT_FOUND`, `VAL_INVALID_INPUT`, `UNAUTHORIZED`, `FORBIDDEN`.

## 3. RFC 9110 ProblemDetails Structure
* Handled by `GlobalExceptionHandlerMiddleware.cs`: returns `application/problem+json` containing `type`, `title`, `status`, `errorCode`, `detail`, `traceId`.

## 4. Enum Persistence & Boundary Serialization
* In PostgreSQL (`category_db`): 100% stored as `integer` (int32). Prohibit `HasConversion<string>()`.
* Enums: `ProductStatus` (`Draft = 0`, `Active = 1`, `Archived = 2`), `CategoryStatus` (`Active = 1`, `Inactive = 2`).
* REST Outbound: 100% serialized as `PascalCase` strings (`"Draft"`, `"Active"`, `"Archived"`).
* REST Inbound: Handled via `PascalCaseJsonStringEnumConverter(allowIntegerValues: false)`.
