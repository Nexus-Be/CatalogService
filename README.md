# CATALOG SERVICE — ARCHITECTURE & FOLDER STRUCTURE GUIDE
## (CẨM NANG CẤU TRÚC THƯ MỤC & PHÁT TRIỂN DÀNH CHO DEVELOPER VÀ AI AGENT)

> **Thông tin dịch vụ**:
> - **Service Port**: `8002` (HTTP/1.1 REST & HTTP/2 gRPC)
> - **Database**: PostgreSQL (`category_db`), Port máy host: `5434`
> - **Nhiệm vụ chính**: Quản lý danh mục, sản phẩm, biến thể (variants), tồn kho kho ảo của Seller sẵn sàng bán.

---

## 1. SƠ ĐỒ PHÂN TẦNG VÀ CẤU TRÚC THƯ MỤC CHUẨN

```
src/Services/Catalog/
│
├── Catalog.Domain/                                    # 1. TẦNG DOMAIN (POCO 100%, ZERO DEPENDENCY)
│   ├── Base/                                          # BaseEntity, IAuditable, ISoftDelete
│   ├── Entities/                                      # Product, Category, ProductVariant, ProductImage...
│   ├── Enums/                                         # ProductStatus, SaleType, InactiveReason...
│   └── Exceptions/                                    # DomainException, BusinessRuleValidationException
│
├── Catalog.Application/                               # 2. TẦNG APPLICATION (USE CASES & BUSINESS LOGIC)
│   ├── Interfaces/                                    # Interfaces của CSDL và các dịch vụ hạ tầng ngoài
│   │   └── ICatalogDbContext.cs                       # Abstraction của DbContext
│   │
│   ├── Common/                                        # Tiện ích và models dùng chung trong Application
│   │
│   ├── Services/                                      # CÁC DỊCH VỤ XỬ LÝ USE CASE (THEO TÍNH NĂNG)
│   │   ├── Product/                                   # Module Quản lý Sản phẩm
│   │   │   ├── IProductService.cs                     # Interface ca sử dụng sản phẩm
│   │   │   ├── ProductService.cs                      # Xử lý nghiệp vụ sản phẩm
│   │   │   └── ProductDtos.cs                         # ProductDto, CreateProductDto...
│   │   └── Category/                                  # Module Quản lý Danh mục
│   │       ├── ICategoryService.cs                    # Interface ca sử dụng danh mục
│   │       ├── CategoryService.cs                     # Xử lý nghiệp vụ danh mục
│   │       └── CategoryDtos.cs                        # CategoryDto...
│   │
│   └── DependencyInjection.cs                         # Đăng ký AddApplicationServices()
│
├── Catalog.Infrastructure/                            # 3. TẦNG INFRASTRUCTURE (CÔNG NGHỆ & HẠ TẦNG CỤ THỂ)
│   ├── Persistence/                                   # Quản lý CSDL PostgreSQL & EF Core 8
│   │   ├── AppDbContext.cs                            # Triển khai ICatalogDbContext
│   │   ├── Configurations/                            # 9 Fluent API Configurations (*Configuration.cs)
│   │   ├── Migrations/                                # Code-first Database Migrations
│   │   └── Seeds/                                     # Dữ liệu hạt giống danh mục mẫu
│   │
│   └── DependencyInjection.cs                         # Đăng ký AddInfrastructureServices(configuration)
│
└── Catalog.Api/                                       # 4. TẦNG API / HOST (ENTRY POINT & MIDDLEWARE)
    ├── Middleware/                                    # MIDDLEWARE TOÀN CỤC CỦA SERVICE
    │   └── GlobalExceptionHandlerMiddleware.cs        # Bắt mọi lỗi ngoại lệ, format RFC 9110 ProblemDetails
    ├── Services/                                      # gRPC Server Service (CatalogGrpcService.cs)
    ├── Contracts/                                     # shop.proto (Hợp đồng gRPC)
    └── Program.cs                                     # Khởi động Kestrel HTTP/1 & HTTP/2, gọi DI App & Infra
```

---

## 2. NGUYÊN TẮC PHÂN CHIA TRÁCH NHIỆM

1. **`Catalog.Domain`**: Chứa các Entity nghiệp vụ sản phẩm, danh mục, đánh giá, biến thể. Tuyệt đối không import thư viện ngoài hay EF Core vào đây.
2. **`Catalog.Application`**: Chứa toàn bộ ca sử dụng (Use Cases). Mỗi tính năng nằm trong thư mục riêng `Services/{Feature}/` gồm DTOs, Interface và Implementation. Gọi CSDL qua `ICatalogDbContext`.
3. **`Catalog.Infrastructure`**: Triển khai `AppDbContext` kế thừa `ICatalogDbContext`. Không dùng Generic Repository bọc ngoài EF Core (ADR 05).
4. **`Catalog.Api`**: Cổng tiếp nhận cực kỳ mỏng. Trang bị `GlobalExceptionHandlerMiddleware` để chuẩn hóa lỗi trả về cho client.

---

## 3. QUY TRÌNH THÊM TÍNH NĂNG MỚI (DÀNH CHO DEV & AI AGENT)

1. **Bước 1**: Tạo Entity & Enum trong `Catalog.Domain/Entities/`.
2. **Bước 2**: Tạo Configuration trong `Catalog.Infrastructure/Persistence/Configurations/` và khai báo `DbSet<T>` trong `ICatalogDbContext` & `AppDbContext`.
3. **Bước 3**: Tạo thư mục tính năng trong `Catalog.Application/Services/{Feature}/` gồm `I{Feature}Service.cs`, `{Feature}Service.cs`, và `{Feature}Dtos.cs`.
4. **Bước 4**: Đăng ký Service vào `Catalog.Application/DependencyInjection.cs`.
5. **Bước 5**: Gọi Service trong `Catalog.Api/Services/CatalogGrpcService.cs`.

---

## 4. QUY CHUẨN THÔNG BÁO LỖI (MANDATORY ERROR RULES)

1. **100% English**: Tuyệt đối không dùng tiếng Việt.
2. **Sentence case**: Viết hoa duy nhất chữ cái đầu (`"Product not found in the catalog."`).
3. **Error Code SCREAMING_SNAKE_CASE**: Ví dụ `PRODUCT_NOT_FOUND`, `CATEGORY_NOT_FOUND`, `INSUFFICIENT_STOCK`.

---

## 5. CÁCH CHẠY VÀ KIỂM THỬ ĐỘC LẬP SERVICE

```bash
# 1. Bật database của Catalog Service (Port 5434):
cd src/Services/Catalog
docker compose up -d catalog-db

# 2. Tạo & Cập nhật migration:
./.tools/dotnet-ef migrations add InitialCatalogSchema --project Catalog.Infrastructure --startup-project Catalog.Api
./.tools/dotnet-ef database update --project Catalog.Infrastructure --startup-project Catalog.Api

# 3. Chạy service:
dotnet run --project Catalog.Api
```
