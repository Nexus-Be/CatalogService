# 03 — SỰ KIỆN BẤT ĐỒNG BỘ QUA RABBITMQ (CATALOG SERVICE EVENT MESSAGING)

> **Trạng thái tài liệu:** ✅ ĐẶC TẢ HOÀN THIỆN — Chuẩn hóa toàn bộ Event Publishing & Consuming theo SRS & Kiến trúc Event-Driven.  
> **Trạng thái hạ tầng (2026-10-28):** ⚠️ MassTransit 8.3.0 + EF Outbox đã cấu hình; Đã kích hoạt chế độ **`UseInMemory: true`** để phát triển và kiểm thử nội bộ cục bộ, có thể chuyển đổi linh hoạt sang container **`nexus-rabbitmq`** trên Docker (`localhost:5672`).  
> **Nguồn sự thật:** `Catalog.Infrastructure/DependencyInjection.cs`, `Catalog.Api/appsettings.json`, SRS Sheet 4 `Catalog Service`.  
> **Cập nhật lần cuối:** 2026-10-28.

---

## 📌 QUY CHUẨN ĐÓNG GÓI SỰ KIỆN (COMMON EVENT ENVELOPE)

Tất cả các sự kiện được phát hành (`Published`) hoặc lắng nghe (`Consumed`) bởi CatalogService đều bắt buộc tuân theo cấu trúc phong bì chuẩn (Envelope) sau để đảm bảo khả năng tương thích toàn hệ thống:

```json
{
  "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventType": "CategoryCreatedEvent",
  "producer": "catalog-service",
  "timestamp": "2026-10-28T16:30:00.000Z",
  "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "causationId": "98765432-dcba-0987-6543-21fedcba0987",
  "version": "1.0",
  "payload": {
    "...": "Dữ liệu nghiệp vụ chi tiết của từng event"
  }
}
```

### Quy ước trường dữ liệu Header
| Trường | Kiểu | Mô tả |
| :--- | :--- | :--- |
| `eventId` | `UUIDv4` | Mã định danh duy nhất của từng message event (Idempotency key). |
| `eventType` | `string` | Tên sự kiện theo chuẩn `PascalCase` kèm hậu tố `Event` (VD: `ProductCreatedEvent`). |
| `producer` | `string` | Tên microservice phát hành sự kiện (luôn là `catalog-service` đối với các event phát đi). |
| `timestamp` | `string` (ISO 8601 UTC) | Thời điểm sự kiện phát sinh chính xác theo múi giờ UTC. |
| `correlationId` | `UUIDv4` | Trace ID đi xuyên suốt các service phục vụ Distributed Tracing & Logging. |
| `causationId` | `UUIDv4` | ID của tác vụ hoặc message trực tiếp gây ra sự kiện này. |
| `version` | `string` | Phiên bản hợp đồng message (mặc định `"1.0"`). |
| `payload` | `object` | Nội dung payload nghiệp vụ tương ứng của từng event. |

---

## 📤 1. DANH MỤC SỰ KIỆN PHÁT HÀNH (PUBLISHED EVENTS)

Dưới đây là danh sách các sự kiện mà CatalogService phát hành ra Message Broker để thông báo cho các Service khác trong hệ sinh thái:

### 1.1. Nhóm Sự kiện Danh mục (Category Events)

#### 1. `CategoryCreatedEvent`
- **Mục đích**: Bắn ra khi Admin tạo mới một danh mục sản phẩm. Giúp `SearchService` đồng bộ cây phân cấp danh mục vào chỉ mục Elasticsearch, và `RecommendationService` cập nhật đồ thị danh mục.
- **Routing Key / Exchange**: `catalog.category.created`
- **Payload Schema**:
```json
{
  "categoryId": "10000000-0000-0000-0000-000000000001",
  "name": "Men's Fashion",
  "slug": "mens-fashion",
  "parentId": null,
  "level": 1,
  "sortOrder": 1,
  "imageUrl": "https://res.cloudinary.com/example/image/upload/v1/categories/cat1.png",
  "isDisabled": false,
  "createdAt": "2026-10-28T16:00:00.000Z",
  "createdBy": "admin-system"
}
```

#### 2. `CategoryUpdatedEvent`
- **Mục đích**: Bắn ra khi Admin chỉnh sửa thông tin danh mục (tên, slug, icon, thứ tự sắp xếp hoặc đổi danh mục cha `parentId`).
- **Routing Key / Exchange**: `catalog.category.updated`
- **Payload Schema**:
```json
{
  "categoryId": "10000000-0000-0000-0000-000000000001",
  "name": "Men's Premium Fashion",
  "slug": "mens-premium-fashion",
  "parentId": null,
  "level": 1,
  "sortOrder": 2,
  "imageUrl": "https://res.cloudinary.com/example/image/upload/v1/categories/cat1_v2.png",
  "isDisabled": false,
  "updatedAt": "2026-10-28T16:30:00.000Z",
  "updatedBy": "admin-system"
}
```

#### 3. `CategoryDeletedEvent`
- **Mục đích**: Bắn ra khi Admin vô hiệu hóa hoặc xóa mềm một danh mục (`IsDeleted = true`). `SearchService` sẽ gỡ bỏ facet lọc danh mục này.
- **Routing Key / Exchange**: `catalog.category.deleted`
- **Payload Schema**:
```json
{
  "categoryId": "10000000-0000-0000-0000-000000000001",
  "name": "Men's Premium Fashion",
  "deletedAt": "2026-10-28T17:00:00.000Z",
  "deletedBy": "admin-system"
}
```

---

### 1.2. Nhóm Sự kiện Sản phẩm & Biến thể (Product & Variant Events)

#### 4. `ProductCreatedEvent` (`CAT-01`, `CAT-43`)
- **Mục đích**: Bắn ra khi Seller tạo mới một sản phẩm. `SearchService` đánh chỉ mục tìm kiếm; `NotificationService` gửi thông báo cho khách hàng theo dõi Shop.
- **Routing Key / Exchange**: `catalog.product.created`
- **Payload Schema**:
```json
{
  "productId": "11000000-0000-0000-0000-000000000001",
  "sellerId": "cccccccc-3333-3333-3333-333333333333",
  "categoryId": "10000000-0000-0000-0000-000000000001",
  "name": "Men Classic Denim Jacket",
  "slug": "men-classic-denim-jacket",
  "brand": "Levis",
  "status": "Active",
  "isAuctionItem": false,
  "minPrice": 1200000.00,
  "maxPrice": 1350000.00,
  "currency": "VND",
  "variants": [
    {
      "variantId": "11100000-0000-0000-0000-000000000001",
      "skuCode": "MCDJ-BLUE-M",
      "name": "Blue / M",
      "price": 1200000.00,
      "availableStock": 100
    }
  ],
  "createdAt": "2026-10-28T10:00:00.000Z"
}
```

#### 5. `ProductUpdatedEvent` (`CAT-02`)
- **Mục đích**: Bắn ra khi Seller cập nhật thông tin chung của sản phẩm (tên, mô tả, thương hiệu, danh mục cha).
- **Routing Key / Exchange**: `catalog.product.updated`
- **Payload Schema**:
```json
{
  "productId": "11000000-0000-0000-0000-000000000001",
  "sellerId": "cccccccc-3333-3333-3333-333333333333",
  "categoryId": "10000000-0000-0000-0000-000000000001",
  "name": "Men Classic Denim Jacket V2",
  "description": "Updated premium material",
  "brand": "Levis",
  "updatedAt": "2026-10-28T11:00:00.000Z"
}
```

#### 6. `ProductStatusChangedEvent` (`CAT-03`)
- **Mục đích**: Bắn ra khi trạng thái sản phẩm chuyển đổi giữa các trạng thái `Draft`, `Pending`, `Active`, `Suspended`, `Disabled`. `SearchService` ẩn/hiện sản phẩm; `CartService` đánh dấu các sản phẩm bị đình chỉ trong giỏ của khách.
- **Routing Key / Exchange**: `catalog.product.status-changed`
- **Payload Schema**:
```json
{
  "productId": "11000000-0000-0000-0000-000000000001",
  "sellerId": "cccccccc-3333-3333-3333-333333333333",
  "oldStatus": "Active",
  "newStatus": "Suspended",
  "reason": "Policy violation investigation",
  "changedAt": "2026-10-28T12:00:00.000Z",
  "changedBy": "admin-moderator"
}
```

#### 7. `ProductDeletedEvent` (`CAT-04`)
- **Mục đích**: Bắn ra khi sản phẩm bị xóa (`IsDeleted = true`). `SearchService` xóa khỏi index, `CartService` tự động loại bỏ khỏi giỏ hàng.
- **Routing Key / Exchange**: `catalog.product.deleted`
- **Payload Schema**:
```json
{
  "productId": "11000000-0000-0000-0000-000000000001",
  "sellerId": "cccccccc-3333-3333-3333-333333333333",
  "isDeleted": true,
  "deletedAt": "2026-10-28T13:00:00.000Z",
  "deletedBy": "seller-user"
}
```

#### 8. `ProductPriceChangedEvent` (`CAT-08`)
- **Mục đích**: Bắn ra khi biến thể hoặc sản phẩm có sự thay đổi giá bán. `NotificationService` gửi push alert "Giảm giá" cho người dùng đã thêm sản phẩm vào wishlist hoặc giỏ hàng.
- **Routing Key / Exchange**: `catalog.product.price-changed`
- **Payload Schema**:
```json
{
  "productId": "11000000-0000-0000-0000-000000000001",
  "variantId": "11100000-0000-0000-0000-000000000001",
  "skuCode": "MCDJ-BLUE-M",
  "oldPrice": 1200000.00,
  "newPrice": 990000.00,
  "currency": "VND",
  "changedAt": "2026-10-28T14:00:00.000Z"
}
```

#### 9. `VariantStatusChangedEvent` (`CAT-09`)
- **Mục đích**: Bắn ra khi một biến thể cụ thể bị vô hiệu hóa (`IsActive = false`) hoặc thay đổi trạng thái bán.
- **Routing Key / Exchange**: `catalog.variant.status-changed`
- **Payload Schema**:
```json
{
  "productId": "11000000-0000-0000-0000-000000000001",
  "variantId": "11100000-0000-0000-0000-000000000001",
  "skuCode": "MCDJ-BLUE-M",
  "isActive": false,
  "status": "Inactive",
  "changedAt": "2026-10-28T14:30:00.000Z"
}
```

---

### 1.3. Nhóm Sự kiện Kho ảo & Checkout Saga

#### 10. `StockReservedEvent`
- **Mục đích**: Phát hành khi CatalogService trừ giữ kho ảo thành công cho một đơn hàng trong Checkout Saga. Kích hoạt bước thanh toán ở `WalletService` / `PaymentService`.
- **Routing Key / Exchange**: `catalog.stock.reserved`
- **Payload Schema**:
```json
{
  "orderId": "ee000000-0000-0000-0000-000000000001",
  "reservationId": "res-9876-abcd-5432",
  "items": [
    {
      "variantId": "11100000-0000-0000-0000-000000000001",
      "skuCode": "MCDJ-BLUE-M",
      "quantity": 2,
      "reservedPrice": 1200000.00
    }
  ],
  "reservedAt": "2026-10-28T15:00:00.000Z",
  "expiresAt": "2026-10-28T15:15:00.000Z"
}
```

#### 11. `StockReservationFailedEvent`
- **Mục đích**: Phát hành khi tồn kho ảo không đủ hoặc sản phẩm bị khóa trong lúc tạo đơn. Kích hoạt `OrderService` / `CommerceService` hủy hoặc từ chối tạo đơn.
- **Routing Key / Exchange**: `catalog.stock.reservation-failed`
- **Payload Schema**:
```json
{
  "orderId": "ee000000-0000-0000-0000-000000000001",
  "reason": "INSUFFICIENT_STOCK",
  "detail": "Requested quantity exceeds available stock.",
  "failedItems": [
    {
      "variantId": "11100000-0000-0000-0000-000000000001",
      "skuCode": "MCDJ-BLUE-M",
      "requestedQuantity": 10,
      "availableQuantity": 2
    }
  ],
  "failedAt": "2026-10-28T15:00:05.000Z"
}
```

#### 12. `StockReleasedEvent`
- **Mục đích**: Phát hành khi CatalogService đã hoàn tất nhả lại số lượng tồn kho ảo (giao dịch bù trừ - Compensating Transaction) khi khách hủy đơn hoặc thanh toán thất bại.
- **Routing Key / Exchange**: `catalog.stock.released`
- **Payload Schema**:
```json
{
  "orderId": "ee000000-0000-0000-0000-000000000001",
  "reservationId": "res-9876-abcd-5432",
  "releasedItems": [
    {
      "variantId": "11100000-0000-0000-0000-000000000001",
      "quantity": 2
    }
  ],
  "reason": "PAYMENT_FAILED_TIMEOUT",
  "releasedAt": "2026-10-28T15:16:00.000Z"
}
```

---

## 📥 2. DANH MỤC SỰ KIỆN LẮNG NGHE (CONSUMED EVENTS)

CatalogService lắng nghe các sự kiện từ các microservice khác để thực hiện đồng bộ trạng thái và ràng buộc toàn vẹn dữ liệu:

### 2.1. `OrderCreatedEvent` (`CAT-32`)
- **Nguồn phát hành**: `CommerceService` / `OrderService`
- **Mục đích nghiệp vụ**: Khi đơn hàng được tạo, CatalogService kiểm tra các biến thể nằm trong đơn hàng và cập nhật trường **`HasOrder = true`** trên bảng `ProductVariant`.
- **Ràng buộc nghiệp vụ**: Biến thể có `HasOrder == true` sẽ vĩnh viễn bị **CẤM XÓA CỨNG** và **CẤM ĐỔI SKU**, chỉ được phép đổi trạng thái (`Inactive`).
- **Payload đầu vào mong đợi**:
```json
{
  "orderId": "ee000000-0000-0000-0000-000000000001",
  "orderNumber": "ORD-20261028-0001",
  "sellerId": "cccccccc-3333-3333-3333-333333333333",
  "customerId": "bbbbbbbb-2222-2222-2222-222222222222",
  "items": [
    {
      "variantId": "11100000-0000-0000-0000-000000000001",
      "productId": "11000000-0000-0000-0000-000000000001",
      "skuCode": "MCDJ-BLUE-M",
      "quantity": 1,
      "price": 1200000.00
    }
  ],
  "createdAt": "2026-10-28T15:00:00.000Z"
}
```

---

### 2.2. `OrderCompletedEvent` (`CAT-33`)
- **Nguồn phát hành**: `CommerceService` / `OrderService` (Khi đơn hàng chuyển sang trạng thái `Delivered` / `Completed`)
- **Mục đích nghiệp vụ**: Lưu bản ghi mua hàng vào bảng `PurchaseRecords` của CatalogService (`CustomerId`, `ProductId`, `VariantId`, `OrderId`, `PurchasedAt`).
- **Ràng buộc nghiệp vụ**: Bảng này là căn cứ duy nhất để xác minh người dùng có đủ điều kiện gửi đánh giá (Review/Rating) cho sản phẩm hay không theo chuẩn `Verified Purchase`.
- **Payload đầu vào mong đợi**:
```json
{
  "orderId": "ee000000-0000-0000-0000-000000000001",
  "customerId": "bbbbbbbb-2222-2222-2222-222222222222",
  "sellerId": "cccccccc-3333-3333-3333-333333333333",
  "completedAt": "2026-10-28T17:00:00.000Z",
  "items": [
    {
      "productId": "11000000-0000-0000-0000-000000000001",
      "variantId": "11100000-0000-0000-0000-000000000001",
      "quantity": 1,
      "price": 1200000.00
    }
  ]
}
```

---

### 2.3. `UserBlockedEvent` / `UserDeletedEvent` (`CAT-42`)
- **Nguồn phát hành**: `IdentityService` / `UserService`
- **Mục đích nghiệp vụ**: Khi một người dùng có role Seller bị quản trị viên khóa tài khoản hoặc xóa tài khoản.
- **Hành động của CatalogService**:
  1. Quét toàn bộ sản phẩm của `SellerId` đó trong cơ sở dữ liệu (`category_db`).
  2. Tự động chuyển đổi `Status` từ `Active` sang `Suspended` (hoặc `Disabled`).
  3. Bắn tiếp sự kiện `ProductStatusChangedEvent` cho từng sản phẩm bị ảnh hưởng để các service ngoại vi (như `SearchService`, `CartService`) đồng bộ ẩn khỏi giao diện khách hàng.
- **Payload đầu vào mong đợi**:
```json
{
  "userId": "cccccccc-3333-3333-3333-333333333333",
  "role": "Seller",
  "action": "Blocked",
  "reason": "Violating commercial fraud terms",
  "blockedAt": "2026-10-28T18:00:00.000Z",
  "blockedBy": "admin-security"
}
```

---

### 2.4. `PaymentFailedEvent`
- **Nguồn phát hành**: `WalletService` / `PaymentService`
- **Mục đích nghiệp vụ**: Xử lý giao dịch bù trừ (Compensating Transaction) trong Checkout Saga khi thanh toán đơn hàng thất bại hoặc hết hạn giữ tiền.
- **Hành động của CatalogService**: Lấy danh sách sản phẩm trong đơn, hoàn trả lại lượng giữ kho ảo và phát hành sự kiện `StockReleasedEvent`.
- **Payload đầu vào mong đợi**:
```json
{
  "orderId": "ee000000-0000-0000-0000-000000000001",
  "paymentId": "pay-12345-6789",
  "reason": "INSUFFICIENT_FUNDS",
  "failedAt": "2026-10-28T15:15:30.000Z"
}
```

---

## ⚙️ 3. CẤU HÌNH HẠ TẦNG MASSTRANSIT (IN-MEMORY vs RABBITMQ)

### 3.1. Cơ chế chuyển đổi In-Memory và RabbitMQ Thật
CatalogService hỗ trợ 2 chế độ vận hành MassTransit linh hoạt được điều khiển qua cấu hình `appsettings.json`:

```json
{
  "RabbitMq": {
    "UseInMemory": true,
    "Host": "localhost",
    "Port": 5672,
    "User": "guest",
    "Password": "<REPLACE_WITH_RABBITMQ_PASSWORD>"
  }
}
```

- **`"UseInMemory": true`** (Mặc định khi dev cục bộ):
  - Khởi tạo MassTransit `UsingInMemory`.
  - Toàn bộ message event được truyền nhận trực tiếp trong bộ nhớ ứng dụng.
  - Phù hợp hoàn hảo để phát triển nhanh, viết unit test/integration test mà không cần khởi động RabbitMQ.
- **`"UseInMemory": false`** (Khi kết nối hạ tầng thật):
  - MassTransit khởi tạo `UsingRabbitMq` kết nối tới broker tại `Host:Port` (Container Docker `nexus-rabbitmq`).
  - Tự động kích hoạt **EF Core Transactional Outbox** trên PostgreSQL (`category_db`) để đảm bảo tính tin cậy tuyệt đối (Guaranteed Delivery), tránh mất mát sự kiện khi có sự cố mạng.

### 3.2. Kiểm tra container `nexus-rabbitmq` trên Docker
Nếu muốn chuyển sang chạy RabbitMQ thật:
1. Xác nhận container `nexus-rabbitmq` đang chạy trên Docker:
   - AMQP Port: `5672`
   - Management UI: `http://localhost:15672` (Tài khoản mặc định: `guest` / `guest`)
2. Đổi `"UseInMemory": false` trong `Catalog.Api/appsettings.json`.
3. Khởi động service `dotnet run --project Catalog.Api`. MassTransit sẽ tự động khai báo Exchanges và Queues theo chuẩn Kebab-case.
