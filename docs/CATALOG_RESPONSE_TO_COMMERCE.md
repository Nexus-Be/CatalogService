# PHẢN HỒI KỸ THUẬT & ĐỐI CHIẾU HỢP ĐỒNG TÍCH HỢP
## CATALOG SERVICE ➔ COMMERCE SERVICE

> **Dự án:** Nexus Microservices Ecosystem  
> **Service gửi phản hồi:** `CatalogService` (Port `8002`, DB PostgreSQL `catalog`)  
> **Service nhận phản hồi:** `CommerceService` (Port `8003`, DB `commerce_db`)  
> **Tài liệu đối chiếu:** `CATALOG_SERVICE_ALIGNMENT_CONTRACT.md` (v1.0.0 Draft)  
> **Ngày lập:** 2026-11-02  
> **Trạng thái:** ✅ SẴN SÀNG TÍCH HỢP NGAY  

---

## 1. TỔNG QUAN KHẢ NĂNG ĐÁP ỨNG

Team Catalog Service đã nghiên cứu kỹ tài liệu dự thảo **`CATALOG_SERVICE_ALIGNMENT_CONTRACT.md`** từ team Commerce. Chúng tôi xin phản hồi như sau:

1. **Về mặt kiến trúc & nghiệp vụ:**
   - ✅ **Bãi bỏ hoàn toàn luồng giữ kho tại Catalog:** Catalog xác nhận **100% không quản lý tồn kho vật lý và không giữ kho**. Commerce làm việc trực tiếp với `FulfillmentService` (Port `8005`) để giữ kho (`reserve`), trừ kho (`commit`) và hoàn kho (`release`).
   - ✅ **Giá tươi thời gian thực (Real-time Fresh Price):** Mọi API nội bộ của Catalog đều truy vấn trực tiếp CSDL (`AsNoTracking()`), không lưu cache giá trễ, đảm bảo Commerce luôn lấy được giá chuẩn nhất để thực thi chính sách **Asymmetric Price Drift** (chặn giá tăng `409`, tự giảm giá rẻ `Auto-adjust`).
   - ✅ **Logic `isPurchasable`:** Đã được cài đặt và tính toán tự động dựa trên trạng thái sản phẩm cha (`Active`), biến thể (`Active`), giá (`Price > 0`), và cờ xóa mềm (`!IsDeleted`).
   - ✅ **Chuẩn định danh & lỗi:** 100% sử dụng **UUID v4 (`Guid`)** và mã lỗi chuẩn **RFC 9110** (`SCREAMING_SNAKE_CASE`).

2. **Về mặt API:**
   - **CatalogService hiện ĐÃ CÓ SẴN các API nội bộ cốt lõi** phục vụ Giỏ hàng (Cart) và Đơn hàng (Order), hoàn toàn có thể sử dụng ngay mà không cần chờ đợi.

---

## 2. CÁC API CATALOG ĐÃ CUNG CẤP & CHI TIẾT CÁC FIELD TRẢ RA

---

### 2.1. API BATCH LẤY THÔNG TIN & GIÁ TƯƠI CỦA NHIỀU SKUs (CHỐNG N+1)
> *Đáp ứng mục 2.2 trong hợp đồng của Commerce (Xem giỏ hàng, tính tiền, checkout quote).*

- **HTTP Method & Route:** `POST /api/v1/internal/variants`
- **Quyền hạn:** `AllowAnonymous` / `InternalMicroservice` (Gọi qua mạng nội bộ)
- **Headers:** `Content-Type: application/json`

#### Request Body (Commerce gửi sang Catalog):
```json
{
  "variantIds": [
    "5fe253bc-97ea-4cbc-960a-33f13be6730e",
    "b187fed0-a82e-4720-b812-ecbc6d4b94c0"
  ]
}
```
*(Giới hạn: Tối đa **100 UUIDs** trong một request để tối ưu hiệu năng).*

#### Response Body thành công (`200 OK` - Đang chạy thực tế):
```json
{
  "items": [
    {
      "variantId": "5fe253bc-97ea-4cbc-960a-33f13be6730e",
      "productId": "1f0ccdfc-a6f4-47e2-bab0-98de9db4c403",
      "skuCode": "DELL-LATITUDE-01",
      "variantName": "Màu Đen - Core i5",
      "productName": "Laptop Dell Latitude 5420",
      "price": 15500000.00,
      "currency": "VND",
      "status": "Active",
      "productStatus": "Active",
      "sellerId": "cccccccc-3333-3333-3333-333333333333",
      "saleType": "Regular",
      "primaryImageUrl": "https://res.cloudinary.com/nexus/image/upload/products/dell-5420.jpg",
      "attributes": [
        {
          "attrName": "Màu Sắc",
          "attrValue": "Đen"
        },
        {
          "attrName": "CPU",
          "attrValue": "Intel Core i5"
        }
      ]
    }
  ],
  "notFoundIds": [
    "00000000-0000-0000-0000-000000000099"
  ]
}
```

#### Bảng ý nghĩa các trường trả ra:
| Tên Field | Kiểu dữ liệu | Ý nghĩa & Hướng dẫn sử dụng cho Commerce |
| :--- | :--- | :--- |
| `variantId` | `UUID` | ID định danh duy nhất của phân loại hàng (SKU ID). |
| `productId` | `UUID` | ID sản phẩm cha. |
| `sellerId` | `UUID` | **Cực kỳ quan trọng:** Dùng để gom nhóm các món trong giỏ theo từng Shop và chia đơn hàng. |
| `productName` | `string` | Tên sản phẩm chính để hiển thị trên item giỏ hàng. |
| `variantName` | `string` | Tên của biến thể/phân loại đã chọn. |
| `skuCode` | `string` | Mã SKU phục vụ quản lý và đối soát vận hành. |
| `price` | `decimal?` | **Đơn giá niêm yết thời gian thực (Fresh Price):** Dùng để tính tổng tiền và so sánh biến động giá (Price Drift). |
| `currency` | `string` | Đơn vị tiền tệ (mặc định: `"VND"`). |
| `status` | `string` | Trạng thái của biến thể (`Active`, `Inactive`). |
| `productStatus` | `string` | Trạng thái sản phẩm cha (`Draft`, `Active`, `Inactive`). |
| `saleType` | `string` | Hình thức bán (`Regular` - hàng thường, `Auction` - đấu giá). |
| `primaryImageUrl` | `string?` | Đường dẫn ảnh thumbnail để hiển thị trong giỏ hàng. |
| `attributes` | `Array` | Danh sách thuộc tính kèm theo (`attrName`, `attrValue`). |
| `notFoundIds` | `Array<UUID>` | **Partial Success:** Danh sách các SKU không tìm thấy. Không quăng lỗi 404 làm chết giỏ hàng của khách. |

---

### 2.2. API BATCH KIỂM TRA ĐIỀU KIỆN MỞ BÁN (`isPurchasable`)
> *Phục vụ trước khi Checkout Quote hoặc Bấm Đặt hàng.*

- **HTTP Method & Route:** `POST /api/v1/internal/variants/check-status`
- **Headers:** `Content-Type: application/json`

#### Request Body:
```json
{
  "variantIds": [
    "5fe253bc-97ea-4cbc-960a-33f13be6730e"
  ],
  "purpose": "CHECKOUT"
}
```
*(Các giá trị `purpose` được hỗ trợ: `"CART"`, `"CHECKOUT"`, `"ORDER"`).*

#### Response Body thành công (`200 OK` - Đang chạy thực tế):
```json
{
  "items": [
    {
      "variantId": "5fe253bc-97ea-4cbc-960a-33f13be6730e",
      "productId": "1f0ccdfc-a6f4-47e2-bab0-98de9db4c403",
      "productStatus": "Active",
      "productStatusReason": null,
      "variantStatus": "Active",
      "variantStatusReason": null,
      "saleType": "Regular",
      "isPurchasable": true,
      "reason": "OK",
      "price": 15500000.00,
      "currency": "VND"
    }
  ],
  "notFoundIds": []
}
```

#### Bảng ý nghĩa các trường trả ra:
| Tên Field | Kiểu dữ liệu | Ý nghĩa |
| :--- | :--- | :--- |
| `isPurchasable` | `boolean` | **`true` nếu đủ điều kiện bán**, `false` nếu bị chặn. |
| `reason` | `string` | Mã lý do nếu `isPurchasable = false`: `"OK"`, `"DELETED"`, `"PRODUCT_NOT_ACTIVE"`, `"VARIANT_NOT_ACTIVE"`, `"PRICE_MISSING"`, `"NOT_FOR_REGULAR_SALE"`. |
| `price` | `decimal?` | Giá niêm yết hiện thời. |

---

### 2.3. API TRA CỨU CHI TIẾT 1 SKU ĐƠN LẺ
> *Đáp ứng mục 2.1 trong hợp đồng của Commerce (Dùng khi khách bấm nhặt 1 món vào giỏ).*

- **HTTP Method & Route:** `GET /api/v1/variants/{id}`
- **Response Body thành công (`200 OK`):**
```json
{
  "id": "5fe253bc-97ea-4cbc-960a-33f13be6730e",
  "productId": "1f0ccdfc-a6f4-47e2-bab0-98de9db4c403",
  "skuCode": "DELL-LATITUDE-01",
  "name": "Màu Đen - Core i5",
  "price": 15500000.00,
  "currency": "VND",
  "isActive": true,
  "hasOrder": false,
  "status": "Active",
  "statusReason": null,
  "attributes": [
    {
      "attrName": "Màu Sắc",
      "attrValue": "Đen"
    }
  ]
}
```

---

### 2.4. XÁC THỰC ĐỔI PHÂN LOẠI HÀNG TRONG GIỎ (VARIANT SWITCH VALIDATION)
> *Đáp ứng mục 2.3 trong hợp đồng của Commerce (Chống gian lận khi khách đổi size/màu trực tiếp trong giỏ).*

- **Khả năng đáp ứng:** ✅ **ĐÃ TRIỂN KHAI HOÀN TẤT & ĐANG CHẠY THỰC TẾ TRÊN PORT 8002**.
- **Cơ chế xác thực của Catalog:**
  1. Nhận `currentSkuId` và `newSkuId`.
  2. Kiểm tra `currentVariant.ProductId == newVariant.ProductId` và cùng `sellerId`.
  3. Nếu khác sản phẩm gốc ➔ Ném lỗi `400 Bad Request` (`CATALOG_VARIANT_MISMATCH`).
  4. Nếu cùng sản phẩm gốc ➔ Tính toán `isPurchasable` của phân loại mới và lấy giá tươi `newUnitPrice`.
- **Hợp đồng Endpoint đang chạy thực tế (Hỗ trợ cả 2 route: POST /api/v1/internal/catalog/skus/validate-variant-switch và POST /api/v1/internal/variants/validate-switch):**
  - **Route:** `POST /api/v1/internal/catalog/skus/validate-variant-switch`
  - **Request Body:**
    ```json
    {
      "currentSkuId": "5fe253bc-97ea-4cbc-960a-33f13be6730e",
      "newSkuId": "b187fed0-a82e-4720-b812-ecbc6d4b94c0"
    }
    ```
  - **Response Body (`200 OK`):**
    ```json
    {
      "isValid": true,
      "productId": "1f0ccdfc-a6f4-47e2-bab0-98de9db4c403",
      "sellerId": "cccccccc-3333-3333-3333-333333333333",
      "newSkuId": "b187fed0-a82e-4720-b812-ecbc6d4b94c0",
      "newSkuName": "Màu Trắng - Core i5",
      "newUnitPrice": 15000000.00,
      "isPurchasable": true,
      "weightGrams": 200
    }
    ```
*(Lưu ý: Nếu Commerce cần dùng ngay trong ngày hôm nay mà chưa cần Catalog mở route mới, Commerce có thể gọi API Batch `POST /api/v1/internal/variants` gửi cả 2 ID để tự so sánh `productId` ngay lập tức).*


---

## 3. BẢNG ĐỐI CHIẾU TRƯỜNG DỮ LIỆU (MAPPING MATRIX)

| Trường Commerce yêu cầu | Trường Catalog đang trả ra | Trạng thái | Ghi chú tích hợp |
| :--- | :--- | :---: | :--- |
| `skuId` | `variantId` / `id` | ✅ Có sẵn | Cùng là UUID của phân loại hàng |
| `productId` | `productId` | ✅ Có sẵn | UUID sản phẩm cha |
| `sellerId` | `sellerId` | ✅ Có sẵn | Đã có trong `POST /api/v1/internal/variants` |
| `productName` | `productName` | ✅ Có sẵn | Đã có trong `POST /api/v1/internal/variants` |
| `skuName` | `variantName` / `name` | ✅ Có sẵn | Tên phân loại hàng |
| `skuCode` | `skuCode` | ✅ Có sẵn | Mã SKU |
| `unitPrice` | `price` | ✅ Có sẵn | Đơn giá niêm yết tươi |
| `thumbnailUrl` | `primaryImageUrl` | ✅ Có sẵn | URL ảnh đại diện |
| `isPurchasable` | `isPurchasable` | ✅ Có sẵn | Đã có trong `POST .../check-status` |
| `status` | `status` | ✅ Có sẵn | Trạng thái biến thể (`Active`, `Inactive`) |
| `attributes` | `attributes` | ✅ Có sẵn | Mảng `{ attrName, attrValue }` |
| `notFoundSkuIds` | `notFoundIds` | ✅ Có sẵn | Mảng các ID không tìm thấy |
| `weightGrams`, `lengthCm`... | Chưa có cột cứng trong DB | ⚠️ Thống nhất | Đề xuất Commerce sử dụng giá trị fallback GHN tiêu chuẩn (`200g`, `10x10x5cm`) |

---

---

> **Đầu mối kỹ thuật CatalogService:** Team Catalog (Port `8002`)  
> **Swagger kiểm chứng trực tiếp:** `http://localhost:8002/swagger`

