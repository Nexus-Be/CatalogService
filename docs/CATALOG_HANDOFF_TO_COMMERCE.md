# Hướng Dẫn Kỹ Thuật & Bàn Giao Tích Hợp API cho Commerce Service

> **Từ:** Catalog Service Backend Engineering Team  
> **Gửi tới:** Commerce Service Backend Engineering Team (Giỏ hàng, Đơn hàng & Checkout)  
> **Đồng kính gửi:** Fulfillment Service Team, Frontend Team, Core Architecture Team  
> **Tài liệu bàn giao:** Tích hợp Cờ Đơn Hàng (`HasOrder`) & Kích Thước Vận Chuyển Chuẩn GHN  
> **Ngày lập:** 19/11/2026  
> **Trạng thái:** ✅ SẴN SÀNG TÍCH HỢP NGAY (Đã build & test pass 100%)

---

## 1. Mục Đích & Bối Cảnh Tích Hợp

Nhằm hoàn thiện tính toàn vẹn dữ liệu cho chu trình đặt hàng (Order Lifecycle) và chuẩn hóa việc tính cước vận chuyển theo nhà xe Giao Hàng Nhanh (GHN), CatalogService trân trọng gửi tới team CommerceService hướng dẫn tích hợp cho 2 hạng mục trọng yếu:

1. **Đồng bộ cờ Đã có Đơn hàng (`HasOrder = true`)**: Khóa quyền xóa vĩnh viễn SKU sau khi phát sinh đơn hàng, bảo vệ tính toàn vẹn dữ liệu kế toán và hóa đơn.
2. **Cung cấp Kích thước đóng gói 3 chiều (`LengthCm`, `WidthCm`, `HeightCm`)**: Tích hợp trực tiếp vào các API Giỏ hàng, Kiểm tra trạng thái và Báo giá vận chuyển (Checkout Quote), loại bỏ kích thước giả định/fallback.

---

## 2. API Đồng Bộ Cờ Đã Có Đơn Hàng (`mark-ordered`)

### 2.1. Nghiệp Vụ & Cơ Chế Hoạt Động
- **Thời điểm gọi**: Khi CommerceService tạo đơn hàng thành công (sự kiện `OrderCreated` / `OrderPlaced` hoặc luồng xử lý Order Checkout).
- **Mục tiêu**: Bật cờ `HasOrder = true` cho toàn bộ danh sách `ProductVariantId` có trong đơn hàng.
- **Quy tắc bảo vệ phía Catalog**:
  - Khi một Variant đã có `HasOrder = true`, Seller **không thể xóa vĩnh viễn** Variant đó (`DELETE /api/v1/variants/{id}` sẽ bị Catalog chặn với mã lỗi `VARIANT_HAS_ORDERS`).
  - Seller chỉ được phép chuyển trạng thái Variant sang `INACTIVE` (với lý do `OUT_OF_STOCK` hoặc `DISCONTINUED`).
  - Dữ liệu lịch sử đơn hàng của người mua và báo cáo doanh thu của sàn được bảo toàn 100%.
- **Tính Idempotent (Chống trùng lặp an toàn)**:
  - Nếu CommerceService gọi lại nhiều lần cho cùng một danh sách `variantIds` (do retry network, worker chạy lại, hoặc cùng 1 sản phẩm được mua nhiều lần), CatalogService xử lý an toàn không phát sinh lỗi, cập nhật trạng thái nếu chưa có và trả về `updatedCount = 0` nếu tất cả đã được đánh dấu từ trước.

---

### 2.2. Chi Tiết Hợp Đồng API

- **Phương thức & Tuyến đường (Route)**:
  - `POST /api/v1/internal/variants/mark-ordered`
  - Alias: `POST /api/v1/internal/catalog/skus/mark-ordered`
- **Mạng gọi**: Mạng nội bộ Docker (`nexus-network`), Service-to-Service gọi trực tiếp tới `http://nexus-catalog-service:8002` (hoặc `http://localhost:5272` khi dev local).
- **Headers**:
  ```http
  Content-Type: application/json
  ```

#### Request Payload (CommerceService gửi sang Catalog):
```json
{
  "variantIds": [
    "b8f6c38a-1234-4567-89ab-cdef01234567",
    "c9a1d2e3-4567-89ab-cdef-0123456789ab"
  ]
}
```

#### Response Payload (`200 OK`):
```json
{
  "updatedCount": 1,
  "updatedVariantIds": [
    "b8f6c38a-1234-4567-89ab-cdef01234567"
  ]
}
```
*Ghi chú:*
- `updatedCount`: Số lượng biến thể thực sự vừa chuyển từ `HasOrder = false` sang `true`.
- `updatedVariantIds`: Danh sách các ID vừa được cập nhật mới trong lần gọi này.

---

## 3. Kích Thước Đóng Gói Chuẩn GHN & Trọng Lượng Tính Cước

### 3.1. Công Thức Quy Đổi Trọng Lượng GHN (Giao Hàng Nhanh)
Giao Hàng Nhanh (GHN API `/shiip/public-api/v2/shipping-order/fee`) quy định khối lượng tính cước vận chuyển dựa trên thể tích đóng gói theo công thức chuẩn:

$$\text{Khối lượng quy đổi (gram)} = \frac{\text{LengthCm} \times \text{WidthCm} \times \text{HeightCm}}{5000} \times 1000$$

$$\text{Khối lượng tính cước} = \max\left(\text{WeightGrams (trọng lượng thực)}, \text{Khối lượng quy đổi}\right)$$

> **Ví dụ thực tế**:
> Sản phẩm áo khoác dạ hoặc mũ bảo hiểm có khối lượng thực chỉ **300g** (`WeightGrams = 300`), nhưng đóng hộp carton kích thước $30 \times 25 \times 20\text{ cm}$:
> $$\text{Khối lượng quy đổi} = \frac{30 \times 25 \times 20}{5000} \times 1000 = 3000\text{ gram (3 kg)}$$
> Khi đó đơn vị vận chuyển GHN sẽ tính cước trên **3.000g** chứ không phải 300g.
> Trước đây khi Catalog thiếu kích thước 3 chiều, hệ thống phải fallback về $20 \times 15 \times 10\text{ cm}$, gây ra tình trạng báo cước cho khách hàng thấp hơn chi phí thực tế thu từ nhà vận chuyển!

---

### 3.2. Cập Nhật Trong Các API Nội Bộ Commerce Đang Sử Dụng

Team CatalogService đã bổ sung đầy đủ bộ 4 trường thông số:
- `weightGrams`: Trọng lượng thực (gram)
- `lengthCm`: Chiều dài gói hàng (cm)
- `widthCm`: Chiều rộng gói hàng (cm)
- `heightCm`: Chiều cao gói hàng (cm)

Vào các API nội bộ mà CommerceService đang tiêu thụ hàng ngày:

#### 1. API Batch lấy thông tin SKUs cho Giỏ hàng (`CAT-24`)
- **Route**: `POST /api/v1/internal/variants`
- **Response**: Mảng `items` bổ sung đầy đủ `lengthCm`, `widthCm`, `heightCm`:
```json
{
  "items": [
    {
      "variantId": "b8f6c38a-1234-4567-89ab-cdef01234567",
      "productId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "skuCode": "HELMET-HJC-RED-XL",
      "variantName": "Đỏ Nhám - Size XL",
      "productName": "Mũ Bảo Hiểm HJC RPHA 11",
      "price": 8500000.00,
      "currency": "VND",
      "status": "Active",
      "productStatus": "Active",
      "sellerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "saleType": "Regular",
      "primaryImageUrl": "https://res.cloudinary.com/nexus/image/upload/v1/helmet.jpg",
      "attributes": [
        { "attrName": "Color", "attrValue": "Red" },
        { "attrName": "Size", "attrValue": "XL" }
      ],
      "isPurchasable": true,
      "weightGrams": 1350,
      "lengthCm": 35,
      "widthCm": 28,
      "heightCm": 26
    }
  ],
  "notFoundIds": []
}
```

---

#### 2. API Kiểm tra Tính Mua Được & Điều Kiện Checkout (`CAT-39`)
- **Route**: `POST /api/v1/internal/variants/check-status`
- **Request Body**: `{ "variantIds": [...], "purpose": "CHECKOUT" }`
- **Response**: Trả về trạng thái mua được kèm thông số kích thước:
```json
{
  "items": [
    {
      "variantId": "b8f6c38a-1234-4567-89ab-cdef01234567",
      "productId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "productStatus": "Active",
      "productStatusReason": null,
      "variantStatus": "Active",
      "variantStatusReason": null,
      "saleType": "Regular",
      "isPurchasable": true,
      "reason": "OK",
      "price": 8500000.00,
      "currency": "VND",
      "weightGrams": 1350,
      "lengthCm": 35,
      "widthCm": 28,
      "heightCm": 26
    }
  ],
  "notFoundIds": []
}
```

---

#### 3. API Xác thực Đổi Biến thể trong Giỏ Hàng (`CAT-47`)
- **Route**: `POST /api/v1/internal/variants/validate-switch`
- **Response**: Trả về `weightGrams`, `lengthCm`, `widthCm`, `heightCm` của SKU mới để CommerceService tính lại cước phí ước tính ngay trên Cart UI nếu sản phẩm thay đổi trọng lượng hoặc thể tích đóng gói.

---

#### 4. API Lấy Chi Tiết Sản Phẩm Nội Bộ (Mới)
- **Route**: `GET /api/v1/internal/products/{id:guid}`
- **Công dụng**: Bỏ qua filter trạng thái công khai, cho phép Commerce tra cứu đầy đủ thông tin sản phẩm và phân cấp biến thể của các sản phẩm đang chờ xử lý hoặc đã mua.

---

## 4. Bảng Tra Cứu Nhanh Đầu Mối API Cho Team Commerce

| Nghiệp Vụ Commerce | Tuyến Đường Catalog | Method | Payload / Chú Thích |
| :--- | :--- | :--- | :--- |
| **Đánh dấu đã mua** | `/api/v1/internal/variants/mark-ordered` | **POST** | `{ "variantIds": [Guid] }` - Gọi khi đơn hàng tạo thành công |
| **Lấy giá tươi & Kích thước Cart** | `/api/v1/internal/variants` | **POST** | `{ "variantIds": [Guid] }` - Nhận đầy đủ Dài/Rộng/Cao/Nặng |
| **Kiểm tra hợp lệ Checkout** | `/api/v1/internal/variants/check-status` | **POST** | `{ "variantIds": [Guid], "purpose": "CHECKOUT" }` |
| **Đổi phân loại trong Cart** | `/api/v1/internal/variants/validate-switch` | **POST** | `{ "currentSkuId": Guid, "newSkuId": Guid }` |
| **Chi tiết 1 biến thể** | `/api/v1/internal/variants/{id}` | **GET** | Trả về `VariantDetailDto` kèm kích thước |
| **Chi tiết sản phẩm** | `/api/v1/internal/products/{id}` | **GET** | Trả về `ProductDetailDto` (không bị chặn theo trạng thái) |

---

## 5. Khuyến Nghị Phối Hợp Kỹ Thuật (Recommendations)

1. **Về luồng `mark-ordered`**:
   - CommerceService nên gọi API `mark-ordered` ngay trong background handler sau khi tạo đơn hàng thành công, hoặc đưa vào Outbox Message để đảm bảo tính sẵn sàng cao (High Availability).
   - Nếu CatalogService gặp sự cố tạm thời, Commerce có thể retry an toàn nhờ cơ chế Idempotent đã được cài đặt sẵn.
2. **Về luồng tính phí giao hàng (Shipping Fee Quote)**:
   - CommerceService có thể áp dụng công thức GHN nêu tại Mục 3.1 để tính tổng khối lượng tính cước của kiện hàng (Package Chargeable Weight) và hiển thị minh bạch cho khách hàng ở bước xem giỏ hàng / xác nhận đơn.

---
*Văn bản chuyển giao được phát hành chính thức bởi Backend Catalog Service Team.*

