# 02 — API ROUTING, ENVELOPES & PAGINATION (CATALOG SERVICE)

## 1. Route Conventions in CatalogService
* Collections: Lowercase plural nouns (`/api/v1/products`, `/api/v1/products/{id}`, `/api/v1/categories`, `/api/v1/categories/{id}`, `/api/v1/variants`).
* Prohibit singular nouns (`/api/v1/product`, `/api/v1/category`).
* One route attribute per action. Use single-line XML `<summary>`.

## 2. Pure DTO Return & Zero ResultFilter
* Controllers return pure DTOs:
  * `Ok(productDto)`
  * `StatusCode(StatusCodes.Status201Created, createdProductDto)`
  * `NoContent()` (for deletion/empty responses)
  * `Ok(pagedData)` (for paginated product/category lists)
* Zero ResultFilter: Do NOT apply `ResultFilter` or manual envelope factories in `Catalog.Api`. Gateway automatically wraps raw DTOs in `ApiResponse<T>`.

## 3. Database Pagination Governance
* Database-level pagination: `.Skip((page - 1) * pageSize).Take(pageSize)` and `.CountAsync()`.
* Output: Standard `PagedData<T>` envelope containing `items` and `pagination` metadata (`pageIndex`, `pageSize`, `totalCount`, `totalPages`, `hasPreviousPage`, `hasNextPage`).
