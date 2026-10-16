# 03 — SỰ KIỆN BẤT ĐỒNG BỘ QUA RABBITMQ (CATALOG SERVICE EVENT MESSAGING)

CatalogService đóng vai trò cốt lõi trong **Checkout Saga State Machine** để đảm bảo tính toàn vẹn kho hàng ảo.

---

## 1. SỰ KIỆN LẮNG NGHE (CONSUMERS)

1. **`OrderSubmittedEvent`** (Do `CommerceService` phát hành khi khách tạo đơn):
   - **Xử lý**: Lặp qua danh sách `Items` trong đơn hàng, kiểm tra `Available` của từng sản phẩm.
   - **Nếu đủ hàng**: Trừ số lượng kho tương ứng $\rightarrow$ Bắn sự kiện `StockReservedEvent`.
   - **Nếu thiếu hàng**: Không trừ kho $\rightarrow$ Bắn sự kiện `StockReservationFailedEvent` (kèm danh sách sản phẩm thiếu).

2. **`PaymentFailedEvent`** (Do `WalletService` phát hành khi thanh toán đơn hàng thất bại):
   - **Xử lý Bù Trừ (Compensating Transaction)**: Lấy lại số lượng sản phẩm trong đơn và cộng hoàn trả lại vào `Available` của kho ảo $\rightarrow$ Bắn sự kiện `StockReleasedEvent`.

---

## 2. SỰ KIỆN PHÁT HÀNH (EVENTS PUBLISHED)

| Tên Sự Kiện | Thời Điểm Kích Hoạt | Payload Dữ Liệu | Services Quan Tâm |
|:---|:---|:---|:---|
| `StockReservedEvent` | Giữ kho thành công cho đơn hàng | `OrderId`, `ReservationId`, `ReservedAt` | `CommerceService`, `WalletService` (Kích hoạt trừ tiền ví). |
| `StockReservationFailedEvent` | Hết hàng hoặc không đủ tồn kho | `OrderId`, `Reason`, `FailedAt` | `CommerceService` (Hủy đơn hàng ngay). |
| `StockReleasedEvent` | Nhả kho hoàn tất khi bù trừ thất bại | `OrderId`, `ReleasedItems`, `ReleasedAt` | `CommerceService`, `AuditService`. |
| `ProductPriceChangedEvent` | Seller cập nhật giá bán mới | `ProductId`, `OldPrice`, `NewPrice`, `UpdatedAt` | `NotificationService` (Báo cho user đã lưu giỏ/yêu thích), `AuditService`. |
