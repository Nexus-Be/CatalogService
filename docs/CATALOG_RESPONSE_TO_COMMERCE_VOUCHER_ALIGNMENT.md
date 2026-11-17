# PHẢN HỒI KỸ THUẬT & ĐỐI CHIẾU HỢP ĐỒNG THẨM ĐỊNH VOUCHER SKU
## CATALOG SERVICE ➔ COMMERCE SERVICE

> **Dự án:** Nexus Microservices Ecosystem  
> **Service gửi phản hồi:** `CatalogService` (Port `8002`, `catalog_db`)  
> **Service nhận phản hồi:** `CommerceService` (Port `8003`, `commerce_db`)  
> **Tài liệu đối chiếu:** `CATALOG_SERVICE_VOUCHER_ALIGNMENT_CONTRACT.md` (2026-11-17)  
> **Ngày lập:** 2026-11-17  
> **Trạng thái:** ✅ SẴN SÀNG 100% — GIỮ NGUYÊN ROUTE HIỆN HỮU, KHÔNG TẠO ROUTE MỚI  

---

## 1. TỔNG QUAN & ĐỒNG THUẬN NGHIỆP VỤ

Team Catalog Service đã nghiên cứu kỹ tài liệu đề xuất **`CATALOG_SERVICE_VOUCHER_ALIGNMENT_CONTRACT.md`** từ team Commerce. Chúng tôi hoàn toàn nhất trí và đánh giá rất cao giải pháp nghiệp vụ của hệ thống Voucher 3 tầng của CommerceService, đặc biệt là 3 cơ chế kiểm tra bảo mật:

1. **Chống gán SKU chéo Shop (`VOUCHER_SELLER_SKU_FORBIDDEN`)**: Bắt buộc `skuInfo.SellerId == currentUserId`. Ngăn chặn triệt để lỗ hổng Shop A tạo voucher giảm giá gán vào sản phẩm của Shop B để phá hoại giá hoặc trục lợi.
2. **Chống gán SKU ngừng bán (`CATALOG_SKU_INACTIVE`)**: Chặn tạo voucher cho SKU có `isPurchasable == false` (bị xóa, ẩn, tạm khóa hoặc chưa kích hoạt).
3. **Kiểm soát biến động giá (Price Drift)**: Tái thẩm định giá tại thời điểm `CreateOrderAsync` và chặn đơn hàng (`409 PRICE_CHANGED`) nếu giá sản phẩm tăng so với lúc báo giá (Quote).

Tuy nhiên, về mặt kỹ thuật triển khai và kiến trúc hệ thống, chúng tôi xin làm rõ và đính chính **3 điểm then chốt** dưới đây.

---

## 2. PHÂN TÍCH & ĐÍNH CHÍNH 3 ĐIỂM KỸ THUẬT

### 2.1. Về Route và Request Payload: GIỮ NGUYÊN ROUTE HIỆN HỮU (KHÔNG TẠO THÊM ROUTE MỚI)

* **Tài liệu đề xuất của Commerce ghi:**
  * Endpoint: `POST /api/v1/internal/skus/batch` (và `GET /api/v1/internal/skus/{skuId}`)
  * Request Body: Mảng thô `["uuid1", "uuid2"]`
  * Response Body: Map/Dictionary `{"uuid1": { ... }}`

* **Hiện trạng thực tế:**
  * CatalogService **ĐÃ CÓ SẴN** endpoint phục vụ việc này từ trước:
    * **`POST /api/v1/internal/variants`**
    * Request Body: `{ "variantIds": ["uuid1", "uuid2"] }`
    * Response Body: `{ "items": [ ... ], "notFoundIds": [ ... ] }`
  * **Bằng chứng trong chính code của CommerceService:** Tại file `Commerce.Infrastructure/Services/Catalog/CatalogClient.cs` (dòng 62-85), client của chính team Commerce **đã và đang gọi chính xác endpoint này**:
    ```csharp
    var payload = new { variantIds = chunk };
    var response = await _httpClient.PostAsJsonAsync("api/v1/internal/variants", payload, JsonOptions, cancellationToken);
    ```
    Sau đó, chính method `GetSkuBatchDetailsAsync` trong `CatalogClient.cs` đã tự map mảng `data.Items` thành `IReadOnlyDictionary<Guid, CatalogProductCheckResult>` để cung cấp cho tầng Application của Commerce.
* **Quyết định kỹ thuật:**
  * Người soạn tài liệu của Commerce đã nhầm lẫn giữa interface hàm C# nội bộ (`CatalogClient.cs`) với REST API contract thực tế.
  * **CatalogService KHÔNG tạo thêm route mới `POST /api/v1/internal/skus/batch`** vì route `POST /api/v1/internal/variants` đã có sẵn, chuẩn RESTful, và code của CommerceService đã tích hợp thành công. Việc tạo thêm route alias sẽ gây phân mảnh và dư thừa.
  * Phía CommerceService chỉ cần tiếp tục sử dụng method `GetSkuBatchDetailsAsync` trong `CatalogClient.cs` là đáp ứng 100% nhu cầu.

---

### 2.2. Về Tồn kho (`stockQuantity`): RÀO CHẮN BOUNDED CONTEXT BẤT BIẾN

* **Tài liệu đề xuất của Commerce ghi:**
  * Yêu cầu CatalogService trả về trường `"stockQuantity": 150`.
  * Checklist ghi: *"Xử lý trả về trạng thái isPurchasable = false nếu sản phẩm bị khóa hoặc hết hàng"*.

* **Quy chuẩn kiến trúc hệ thống:**
  * **CatalogService KHÔNG quản lý tồn kho vật lý.** Cơ sở dữ liệu của CatalogService không có bảng `inventories` và không có trường `stockQuantity`.
  * Toàn bộ tồn kho vật lý, số lượng khả dụng (`available`), trừ kho/giữ kho thuộc 100% quyền quản lý của **FulfillmentService** (Port `8006`).
  * Cờ `isPurchasable` của CatalogService là kiểm tra nghiệp vụ cấp Catalog:
    * Sản phẩm cha: `Status == ProductStatus.Active` và `!IsDeleted`
    * Phiên bản (Variant): `Status == VariantStatus.Active` và `!IsDeleted`
    * Giá bán: `Price.HasValue && Price.Value > 0`
  * CatalogService **không thể và không được phép** kiểm tra số lượng tồn kho vật lý.
* **Quyết định kỹ thuật:**
  * CommerceService **bỏ trường `stockQuantity`** khỏi hợp đồng thẩm định với CatalogService.
  * Nếu CommerceService muốn kiểm tra số lượng tồn kho vật lý khi áp dụng voucher hoặc tạo đơn, CommerceService **bắt buộc phải gọi sang FulfillmentService** (`IFulfillmentClient`), tuân thủ đúng Bounded Context.

---

### 2.3. Bổ sung ràng buộc bắt buộc: CHẶN VOUCHER ĐỐI VỚI SẢN PHẨM ĐẤU GIÁ (`SaleType == Auction`)

* Trong response của `POST /api/v1/internal/variants`, CatalogService luôn trả về trường:
  * `saleType: "Regular" | "Auction"`
* **Quy tắc bắt buộc đề nghị CommerceService bổ sung vào Mục 3:**
  * Voucher Sản phẩm (`ScopeType = Product`) **CHỈ ĐƯỢC PHÉP ÁP DỤNG cho `SaleType == "Regular"`**.
  * **Nghiêm cấm** áp dụng voucher cho sản phẩm đấu giá (`SaleType == "Auction"`), vì giá bán đấu giá do các lượt bid xác định, không được giảm giá bằng voucher thương mại.
  * Nếu SKU gửi lên có `saleType == "Auction"`, CommerceService cần chặn và ném lỗi:
    * **HTTP 400 `VOUCHER_SKU_AUCTION_NOT_ALLOWED`**
    * Message: `"Voucher cannot be applied to auction products."`

---

## 3. BẢNG ĐỐI CHIẾU TRƯỜNG DỮ LIỆU CHUẨN GIỮA HAI SERVICE

CatalogService qua endpoint `POST /api/v1/internal/variants` đã cung cấp đầy đủ 100% dữ liệu để `CatalogClient.cs` của CommerceService map sang model thẩm định Voucher:

| Trường trong Contract dự thảo | Trường CatalogService trả về (`BatchVariantItemDto`) | Kiểu dữ liệu | Ý nghĩa nghiệp vụ |
|:---|:---|:---|:---|
| `skuId` | `VariantId` | `Guid` (UUID v4) | Mã định danh phiên bản |
| *(không có)* | `SkuCode` | `string` | Mã SKU định danh người bán |
| `productId` | `ProductId` | `Guid` (UUID v4) | Mã sản phẩm cha |
| `sellerId` | `SellerId` | `Guid` (UUID v4) | **Dùng để đối chiếu quyền sở hữu Shop (`skuInfo.SellerId == currentUserId`)** |
| `productName` | `ProductName` | `string` | Tên hiển thị sản phẩm cha |
| *(không có)* | `VariantName` | `string` | Tên phân loại phiên bản |
| `unitPrice` | `Price` | `decimal?` | **Giá tươi thời gian thực (Real-time Fresh Price)** |
| *(không có)* | `Currency` | `string` | Tiền tệ (`"VND"`) |
| `isPurchasable` | `IsPurchasable` | `bool` | `true` khi cả Product & Variant đều `Active` và có giá bán hợp lệ |
| *(không có)* | `SaleType` | `string` | **`"Regular"` hoặc `"Auction"` — dùng để chặn voucher gán vào sản phẩm đấu giá** |
| *(không có)* | `Status` | `string` | Trạng thái variant (`"Active"`, `"Inactive"`...) |
| *(không có)* | `ProductStatus` | `string` | Trạng thái sản phẩm cha (`"Active"`, `"Suspended"`, `"Banned"`...) |
| `stockQuantity` | ❌ **Không trả** | — | **Thuộc quyền quản lý của FulfillmentService (Port 8006)** |
| `errorReason` | Danh sách `NotFoundIds` | `List<Guid>` | Các SKU ID không tìm thấy trong hệ thống |

---

## 4. KẾT LUẬN & KHUYẾN NGHỊ THỰC HIỆN

1. **Phía CatalogService:**
   - Đã sẵn sàng 100% tại `POST /api/v1/internal/variants`.
   - Giữ nguyên thiết kế RESTful hiện có, không bổ sung thêm endpoint alias nào để đảm bảo hệ thống gọn gàng, nhất quán.
2. **Phía CommerceService:**
   - Cập nhật lại tài liệu `CATALOG_SERVICE_VOUCHER_ALIGNMENT_CONTRACT.md` cho khớp với code thực tế của `CatalogClient.cs` (`POST /api/v1/internal/variants` với payload `{ variantIds: [...] }`).
   - Bỏ yêu cầu trường `stockQuantity` khỏi Catalog contract; gọi FulfillmentService nếu cần kiểm tra tồn kho.
   - Bổ sung validation chặn voucher cho sản phẩm có `saleType == "Auction"`.
