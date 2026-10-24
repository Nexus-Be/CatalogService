# CATALOG SERVICE SPEC KIT — CHỈ DẪN KỸ THUẬT CHO AI AGENT & DEVELOPER

> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — 6/6 file spec đã được đối chiếu lại với code; hiện trạng repo: **0/13 rpc gRPC**, **13 action REST mock**, **9 bảng nghiệp vụ + 3 bảng outbox**, **0 event**, **0 test**.
> **Nguồn sự thật:** code trong repo này (bằng chứng `file:dòng`); không tham chiếu tài liệu ngoài repo.
> **Cập nhật lần cuối:** 2026-10-24.

Tài liệu này là **Bộ Chỉ Dẫn & Đặc Tả Kỹ Thuật (Spec Kit)** cho repository **CatalogService** thuộc hệ sinh thái Microservices **Nexus**. Giúp mọi lập trình viên và AI Coding Agent (Cursor, Claude, Copilot, DeepSeek...) nắm bắt bối cảnh ngay lập tức, hiểu rõ ranh giới nghiệp vụ về danh mục, sản phẩm, kho ảo và **loại bỏ hoàn toàn nguy cơ ảo giác (zero hallucination)**.

---

## 1. MỤC LỤC BỘ ĐẶC TẢ CATALOG SERVICE (SPECS/)

| File Đặc Tả | Trọng Tâm Nội Dung | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|
| [00_AGENT_MISSION_GUARDRAILS.md](00_AGENT_MISSION_GUARDRAILS.md) | Bounded Context, Port `8002` (Docker) / `5272` (local), DB `category_db` (`5434`), các điều cấm kỵ cốt lõi. | ⚠️ Một phần (guardrail đúng, phần gRPC chỉ là mục tiêu) | `specs/00_AGENT_MISSION_GUARDRAILS.md:20-27` |
| [01_CLEAN_ARCHITECTURE_LAYERS.md](01_CLEAN_ARCHITECTURE_LAYERS.md) | Cấu trúc 4 tầng Clean Architecture (`.Api`, `.Application`, `.Domain`, `.Infrastructure`), luồng phụ thuộc. | ⚠️ Một phần (Application chưa được API dùng; Domain thiếu `Inventory`/`Money`) | `Catalog.sln`, `Catalog.Application/DependencyInjection.cs:11-12` |
| [02_API_GRPC_CONTRACTS.md](02_API_GRPC_CONTRACTS.md) | Đặc tả gRPC `CatalogApi` (13 rpc), Request/Response messages, phân trang, mã lỗi. | ❌ Chưa có gRPC (0/13); bề mặt thật là REST mock | `Catalog.Api/Controllers/*.cs` |
| [03_EVENT_MESSAGING.md](03_EVENT_MESSAGING.md) | Sự kiện bất đồng bộ RabbitMQ + MassTransit **8.3.0** (Giữ kho ảo trong Checkout Saga, nhả kho khi bù trừ). | ❌ Chưa có consumer/publisher (chỉ đăng ký hạ tầng) | `Catalog.Infrastructure/DependencyInjection.cs:23-48` |
| [04_DATA_PERSISTENCE.md](04_DATA_PERSISTENCE.md) | 9 thực thể thật (`Category`, `Product`, `ProductVariant`, …), DbContext EF Core 8 (No Repository Pattern). ❌ Không có `Inventory`. | ⚠️ Một phần (12 bảng tồn tại, nhưng API không đọc DB) | `Catalog.Infrastructure/Persistence/AppDbContext.cs:14-33` |
| [05_DEV_VERIFICATION_CHECKLIST.md](05_DEV_VERIFICATION_CHECKLIST.md) | Lệnh biên dịch, **smoke test thủ công** (không có test tự động), checklist trước khi commit. | ⚠️ Một phần (0 project test) | `Catalog.sln` |

---

## 2. THÔNG SỐ VẬN HÀNH CHÍNH

- **Ngôn ngữ & Runtime**: .NET 8 (`net8.0` — 4/4 `.csproj`), C# 12.
- **Cổng dịch vụ**: `8002` khi chạy Docker (`Dockerfile:3-4`, `docker-compose.yml:37`); `5272` (http) / `7232` (https) khi `dotnet run` local (`Catalog.Api/Properties/launchSettings.json:17,27`).
- **Bề mặt giao tiếp**: ✅ REST `/api/v1/catalog/{products,categories,inventory}` (13 action, mock in-memory) — ❌ **không có gRPC**, nên không có "cổng gRPC Server". (Tài liệu cũ ghi "Không có cổng HTTP REST riêng" là **sai**.)
- **Cơ Sở Dữ Liệu**: PostgreSQL 16, port `5434`, database `category_db` (`docker-compose.yml:15-19`). ⚠️ Mật khẩu dev đang plaintext trong `Catalog.Api/appsettings.json:10`.
- **Message Broker**: RabbitMQ port `5672`, virtual host mặc định của MassTransit (không đặt `VirtualHost` tường minh — `Catalog.Infrastructure/DependencyInjection.cs:23-48`); ⚠️ chưa có consumer/publisher nào dùng tới.
- **Đơn Vị Tiền Tệ Mặc Định**: `VND` — khai báo trên `ProductVariant.Currency = "VND"` (`Catalog.Domain/Entities/ProductVariant.cs:12`); giá là `decimal?` precision `(18,2)` (`ProductVariantConfiguration.cs`). ⚠️ `Product` **không có** trường `Price`/`Currency`.
- **Phiên bản thư viện chính** (lấy từ `.csproj`): `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.10, `Microsoft.EntityFrameworkCore` 8.0.10, `MassTransit` / `MassTransit.RabbitMQ` / `MassTransit.EntityFrameworkCore` **8.3.0** (tài liệu cũ ghi 8.4.1 là **sai**), `Swashbuckle.AspNetCore` 6.6.2, `Microsoft.AspNetCore.OpenApi` 8.0.29. Không có package `Grpc.*`, không có JWT, không có OpenTelemetry.
