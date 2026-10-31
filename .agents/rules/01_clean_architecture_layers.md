# 01 — CLEAN ARCHITECTURE & BOUNDED CONTEXT (CATALOG SERVICE)

## 1. Bounded Context & Port Allocation
* Bounded Context: Product catalog, product variants, hierarchical categories, virtual inventory linkage.
* Service Port: `8002` (HTTP REST API).
* Database: PostgreSQL `category_db` on port `5434`.

## 2. 4-Layer Clean Architecture
* `Catalog.Domain`: Entities (`Product`, `Category`, `ProductVariant`), Enums (`ProductStatus`, `CategoryStatus`), Exceptions (`CatalogDomainException`).
* `Catalog.Application`: Use cases in `Services/{Feature}/`, interface `ICatalogDbContext`, request/response records, typed client interfaces (`Interfaces/IFulfillmentClient.cs`).
* `Catalog.Infrastructure`: `AppDbContext`, EF Core configurations, migrations, and typed REST client `FulfillmentClient`.
* `Catalog.Api`: Thin controllers in `Controllers/Public/`, middlewares, extensions, composition root (`Program.cs`).

## 3. Feature Co-Location (Mandatory)
* `Services/Product/`: `IProductService.cs`, `ProductService.cs`, `ProductRequests.cs`, `ProductResponses.cs`.
* `Services/Category/`: `ICategoryService.cs`, `CategoryService.cs`, `CategoryRequests.cs`, `CategoryResponses.cs`.

## 4. Thin 1-to-1 Controller Pairing
* `ProductsController.cs` -> injects `IProductService`.
* `CategoriesController.cs` -> injects `ICategoryService`.

## 5. Absolute Architectural Prohibitions
* 🛑 Prohibit Repository Pattern over EF Core (inject `ICatalogDbContext` directly).
* 🛑 Prohibit MediatR & CQRS handlers.
* 🛑 Prohibit gRPC and Message Brokers (RabbitMQ/MassTransit).
* 🛑 Prohibit direct queries to other microservice databases.
