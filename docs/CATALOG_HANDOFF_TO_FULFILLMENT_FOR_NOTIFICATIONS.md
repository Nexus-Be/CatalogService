# Văn bản Chuyển giao Kỹ thuật: Tích hợp Thông báo Cảnh báo Tồn kho Thấp (Low Stock Alerts) sang FulfillmentService

> **Từ:** Catalog Service Backend Engineering Team  
> **Gửi tới:** Fulfillment Service Backend Engineering Team  
> **Đồng kính gửi:** Notification Service Team, Frontend Team, Core Architecture Team  
> **Tài liệu tham chiếu:** `CATALOG_NOTIFICATION_HANDOFF.md` (Do Notification Team gửi ngày 18/11/2026)  
> **Ngày lập:** 19/11/2026  
> **Mức độ ưu tiên:** Quan trọng (High) — Định tuyến đúng Bounded Context & Trách nhiệm Dịch vụ

---

## 1. Lý do Chuyển giao & Ranh giới Nghiệp vụ (Context & Justification)

Vào ngày 18/11/2026, team Notification đã gửi tài liệu kỹ thuật `CATALOG_NOTIFICATION_HANDOFF.md` đề xuất tích hợp API cảnh báo tồn kho thấp:
- `POST /api/v1/notifications/low-stock-alerts`

CatalogService xin làm rõ ranh giới nghiệp vụ (Domain Bounded Context) và chính thức chuyển giao trách nhiệm kích hoạt tính năng này sang **FulfillmentService**:

### 🏢 Bounded Context Thực tế trong Kiến trúc Hệ thống:
1. **CatalogService (Kho ảo / Bán hàng)**:
   - Chỉ quản lý thông tin danh mục, sản phẩm, biến thể, hình ảnh, đánh giá và báo cáo vi phạm.
   - Catalog **KHÔNG** quản lý kho hàng vật lý, không quản lý số lượng tồn kho thực tế trong từng ngăn kệ (Bin/Shelf), không quản lý nhập kho (Inbound), xuất kho (Outbound), và ngưỡng tồn kho an toàn (Safety Stock Threshold).
2. **FulfillmentService (Kho hàng vật lý & Vận hành giao vận)**:
   - **Sở hữu 100% dữ liệu tồn kho vật lý**: Bảng `Inventories` / `WarehouseStock` với các chỉ số:
     - `OnHand` (Số lượng hàng đang có thực tế trong kho)
     - `Reserved` / `Allocated` (Số lượng đã phân bổ cho các đơn hàng đang chuẩn bị đóng gói)
     - `Available` (Số lượng khả dụng sẵn sàng xuất bán)
     - `SafetyStockThreshold` / `MinStockLevel` (Ngưỡng tồn kho an toàn do Seller hoặc thủ kho thiết lập)
   - Do đó, **chỉ có FulfillmentService mới có dữ liệu thời gian thực để biết khi nào một SKU chạm ngưỡng cảnh báo tồn kho thấp**!

---

## 2. Trách nhiệm Triển khai của FulfillmentService

### 2.1. Logic Phát hiện Tồn kho Thấp (Detection Trigger)
Khi có bất kỳ giao dịch nào làm giảm lượng tồn kho khả dụng tại FulfillmentService:
- Đơn hàng mới được giữ hàng (`AllocateInventory` / `DeductStock`).
- Xuất kho thành công (`OutboundCompleted`).
- Kiểm kê phát hiện hàng hỏng/hụt kho (`StockAdjustment`).

FulfillmentService thực hiện kiểm tra:
$$\text{AvailableQuantity} \le \text{SafetyStockThreshold}$$

Nếu điều kiện trên thỏa mãn (và chưa có cảnh báo nào được gửi trong chu kỳ giãn cách, ví dụ 24h qua):
FulfillmentService sẽ gọi API sang NotificationService hoặc bắn Event qua RabbitMQ/MassTransit.

---

### 2.2. Hợp đồng API Gọi sang NotificationService

FulfillmentService sẽ inject HTTP Client (`INotificationClient`) hoặc sử dụng MassTransit Publisher để gửi payload:

- **Endpoint của NotificationService**: `POST /api/v1/notifications/low-stock-alerts`
- **Request Body (JSON)**:
```json
{
  "sellerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "skuCode": "HELMET-FULLFACE-RED-L",
  "productName": "Mũ Bảo Hiểm Fullface HJC RPHA 11",
  "currentStock": 3,
  "threshold": 5,
  "warehouseId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "warehouseName": "Kho Tổng Bình Dương",
  "timestamp": "2026-11-19T09:30:00Z"
}
```

- **Xử lý phản hồi**:
  - `200 OK` / `202 Accepted`: NotificationService tiếp nhận và đưa vào hàng đợi gửi Email/In-App thông báo cho Seller.
  - Xử lý Retry với Resilience Policy (Polly / Exponential Backoff) nếu NotificationService bận.

---

## 3. Phần CatalogService đã Hỗ trợ Sẵn sàng cho FulfillmentService

Để hỗ trợ FulfillmentService tối đa trong việc quản lý kho và giao vận với đơn vị vận chuyển GHN (Giao Hàng Nhanh), CatalogService đã hoàn tất các nâng cấp sau:

### 3.1. Kích thước Hộp hàng Thực tế (`LengthCm`, `WidthCm`, `HeightCm`)
- **Trước đây**: Catalog chỉ có `WeightGrams`. FulfillmentService khi gọi GHN tính phí vận chuyển (`/shiip/public-api/v2/shipping-order/fee`) đã phải hardcode kích thước đóng gói mặc định là:
  $$\text{Dài} = 20\text{ cm}, \quad \text{Rộng} = 15\text{ cm}, \quad \text{Cao} = 10\text{ cm}$$
  Điều này dẫn đến cước phí tính cho khách hàng bị sai lệch đối với hàng cồng kềnh.
- **Hiện tại**: CatalogService đã cập nhật schema cơ sở dữ liệu và API:
  - Lưu trữ `LengthCm`, `WidthCm`, `HeightCm` trên cả `Product` và `ProductVariant`.
  - Hỗ trợ công thức tính khối lượng quy đổi chuẩn GHN:
    $$\text{Khối lượng tính cước} = \max\left(\text{WeightGrams}, \frac{\text{Length} \times \text{Width} \times \text{Height}}{5000} \times 1000\right)$$

### 3.2. Endpoint Nội bộ Đồng bộ Thông tin cho FulfillmentService
Khi FulfillmentService cần lấy đầy đủ thông tin sản phẩm và kích thước để tạo phiếu nhập kho hoặc tính cước GHN, hãy sử dụng endpoint nội bộ:
- `GET /api/v1/internal/products/{id:guid}`
- `GET /api/v1/internal/variants/{id:guid}` (hoặc batch: `POST /api/v1/internal/variants`)

Các endpoint này không bị lọc bởi trạng thái bán hàng công khai (`filterActiveOnly=false`) và trả về đầy đủ `LengthCm`, `WidthCm`, `HeightCm`, `WeightGrams`.

---

## 4. Kế hoạch Phối hợp Tiếp theo (Next Steps)

1. **Fulfillment Team**:
   - Tiếp nhận tài liệu `CATALOG_NOTIFICATION_HANDOFF.md`.
   - Bổ sung cấu hình `SafetyStockThreshold` vào bảng quản lý tồn kho tại FulfillmentService (nếu chưa có).
   - Tích hợp gọi `POST /api/v1/notifications/low-stock-alerts` vào luồng trừ kho / kiểm kê.
2. **Catalog Team**:
   - Sẵn sàng hỗ trợ test tích hợp và đảm bảo các endpoint internal luôn ổn định 100%.

---
*Văn bản chuyển giao được phát hành chính thức bởi Backend Catalog Service Team.*

