# 03 — SỰ KIỆN BẤT ĐỒNG BỘ QUA RABBITMQ (CATALOG SERVICE EVENT MESSAGING)

> **Trạng thái triển khai (2026-10-24):** ❌ **0/2 consumer, 0/4 event publish** — chỉ có hạ tầng MassTransit + EF Outbox được đăng ký, **không có consumer, không có lệnh publish nào**, không có contract message nào trong repo.
> **Nguồn sự thật:** code trong repo này (bằng chứng `file:dòng`); không tham chiếu tài liệu ngoài repo.
> **Cập nhật lần cuối:** 2026-10-24.

CatalogService **được đặc tả** đóng vai trò cốt lõi trong **Checkout Saga State Machine** để đảm bảo tính toàn vẹn kho hàng ảo. Phần dưới đây là **đặc tả mục tiêu**; hiện trạng đối chiếu ở mục 3.

---

## 1. SỰ KIỆN LẮNG NGHE (CONSUMERS)

1. **`OrderSubmittedEvent`** (Do `CommerceService` phát hành khi khách tạo đơn):
   - **Xử lý**: Lặp qua danh sách `Items` trong đơn hàng, kiểm tra `Available` của từng sản phẩm.
   - **Nếu đủ hàng**: Trừ số lượng kho tương ứng $\rightarrow$ Bắn sự kiện `StockReservedEvent`.
   - **Nếu thiếu hàng**: Không trừ kho $\rightarrow$ Bắn sự kiện `StockReservationFailedEvent` (kèm danh sách sản phẩm thiếu).

2. **`PaymentFailedEvent`** (Do `WalletService` phát hành khi thanh toán đơn hàng thất bại):
   - **Xử lý Bù Trừ (Compensating Transaction)**: Lấy lại số lượng sản phẩm trong đơn và cộng hoàn trả lại vào `Available` của kho ảo $\rightarrow$ Bắn sự kiện `StockReleasedEvent`.

> ❌ **Cả 2 consumer trên chưa tồn tại.** Không có class nào triển khai `IConsumer<T>` trong repo; không có thư mục `Consumers/` hay `Contracts/`. `ConfigureEndpoints` được gọi nhưng không có endpoint nào để cấu hình (`Catalog.Infrastructure/DependencyInjection.cs:44`).

---

## 2. SỰ KIỆN PHÁT HÀNH (EVENTS PUBLISHED)

| Tên Sự Kiện | Thời Điểm Kích Hoạt | Payload Dữ Liệu | Services Quan Tâm | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|:---|:---|
| `StockReservedEvent` | Giữ kho thành công cho đơn hàng | `OrderId`, `ReservationId`, `ReservedAt` | `CommerceService`, `WalletService` (Kích hoạt trừ tiền ví). | ❌ Chưa có | 0 `IPublishEndpoint` trong repo |
| `StockReservationFailedEvent` | Hết hàng hoặc không đủ tồn kho | `OrderId`, `Reason`, `FailedAt` | `CommerceService` (Hủy đơn hàng ngay). | ❌ Chưa có | 0 `IPublishEndpoint` trong repo |
| `StockReleasedEvent` | Nhả kho hoàn tất khi bù trừ thất bại | `OrderId`, `ReleasedItems`, `ReleasedAt` | `CommerceService`, `LogService`. | ❌ Chưa có | 0 `IPublishEndpoint` trong repo |
| `ProductPriceChangedEvent` | Seller cập nhật giá bán mới | `ProductId`, `OldPrice`, `NewPrice`, `UpdatedAt` | `NotificationService` (Báo cho user đã lưu giỏ/yêu thích), `LogService`. | ❌ Chưa có | 0 `IPublishEndpoint` trong repo; `UpdateProduct` chỉ sửa `Name/Description/Brand/Status` (`ProductsController.cs:11`) và **không có** nhánh cập nhật giá |

**Ghi chú**: `ProductPriceChangedEvent` giả định có thao tác "seller cập nhật giá", nhưng API hiện tại **không có** endpoint hay request field nào cho giá — giá nằm ở `ProductVariant.Price` và chỉ được seed (`Catalog.Infrastructure/Persistence/Seeds/CatalogSeedData.cs:174-258`).

---

## 3. HIỆN TRẠNG HẠ TẦNG MESSAGING

| Hạng mục | Trạng thái | Bằng chứng |
|---|---|---|
| Package MassTransit 8.3.0 (+ RabbitMQ, + EntityFramework) | ✅ Có | `Catalog.Infrastructure/Catalog.Infrastructure.csproj` |
| Đăng ký `AddMassTransit` + `UsingRabbitMq` | ✅ Có (host/port/user/pass từ config, mặc định `localhost:5672` / `guest`) | `Catalog.Infrastructure/DependencyInjection.cs:23-48` |
| EF Outbox (`AddEntityFrameworkOutbox`, `UsePostgres`, `UseBusOutbox`) | ✅ Có | `DependencyInjection.cs:25-30`; bảng `InboxState`/`OutboxState`/`OutboxMessage` có trong migration |
| Consumer thật | ❌ Chưa có | `grep IConsumer` = 0 |
| Publisher thật | ❌ Chưa có | `grep IPublishEndpoint` = 0 |
| Contract/DTO sự kiện dùng chung | ❌ Chưa có | không có thư mục `Contracts/` |
| Cấu hình RabbitMQ trong `appsettings.json` | ⚠️ Có nhưng chứa mật khẩu thật, và chưa có consumer nào dùng tới | `Catalog.Api/appsettings.json:12-17` |
