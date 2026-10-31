# 03 — SECURITY HEADERS, INTERNAL API & IDENTITY (CATALOG SERVICE)

## 1. Controller Segregation
* `Catalog.Api/Controllers/Public/`: Serves public product/category catalog requests.
* `Catalog.Api/Controllers/Internal/`: Serves inter-service internal endpoints (`/api/v1/internal/...`).

## 2. Internal Service Security
* All internal endpoints protected by `InternalServiceAuthMiddleware` with `X-Internal-Service-Key`.
* Outbound client `FulfillmentInternalClient` registered with `InternalServiceAuthHandler` to call Fulfillment internal inventory init endpoint `POST /api/v1/internal/inventories/initialize`.

## 3. Trusted Identity Extraction & Ownership Guard
* When creating or editing products, extract `SellerId` from `this.GetUserId()`.
* Check resource ownership: seller can only edit their own products. Admin can edit any product.
* Use `TryGetUserId` and `EnsureOwnership` from `Catalog.Api.Extensions.HttpContextExtensions`.
