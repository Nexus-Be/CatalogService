# 03 — QUY CHUẨN GIAO TIẾP VÀ BÃI BỎ MESSAGE BROKER (CATALOG SERVICE)

> **Trạng thái triển khai (2026-10-30):** 🛑 ĐÃ BÃI BỎ HOÀN TOÀN RABBITMQ & MASSTRANSIT — Toàn bộ cơ chế Consumer, Publisher, Outbox tables và Saga giữ kho phân tán đã được bãi bỏ. Giao tiếp tồn kho hoặc kiểm tra sản phẩm chuyển sang REST API đồng bộ.
> **Nguồn sự thật:** code (`Catalog.Api/Program.cs`).
> **Cập nhật lần cuối:** 2026-10-30.

---

## 1. NGUYÊN TẮC BÃI BỎ MESSAGE BROKER

1. **Không sử dụng RabbitMQ hay MassTransit**:
   - Toàn bộ các package `MassTransit`, `MassTransit.RabbitMQ`, `MassTransit.EntityFrameworkCore` đã được loại bỏ khỏi CatalogService.
   - Không khai báo bất kỳ consumer hoặc publisher nào.

2. **Giao tiếp liên dịch vụ qua REST API**:
   - Các dịch vụ khác (như `CommerceService`) khi cần kiểm tra hoặc giữ tồn kho gọi trực tiếp endpoint REST của CatalogService.
