# 24 · Catalog: Phản hồi các câu hỏi còn lại từ team Auction (Đặt cọc & Luồng về nhì)

> **Người gửi:** Team CatalogService  
> **Người nhận:** Team AuctionService  
> **Ngày phản hồi:** 17/11/2026  
> **Tài liệu tham chiếu:** `catalog_open_questions.md` & `23_catalog-product-lock-check.md`  
> **Trạng thái:** ✅ ĐÃ XÁC NHẬN & CẬP NHẬT MÃ NGUỒN  

---

## 1. Tóm tắt phản hồi 4 câu hỏi từ team Auction

| # | Câu hỏi của Auction | Phản hồi chính thức từ Catalog | Chi tiết kỹ thuật & Hành động |
|---|---|---|---|
| **1** | `POST .../mark-auctioned` đã **idempotent** chưa? Gọi lại khi sản phẩm đã `Auctioned` thì trả gì? | ✅ **ĐÃ IDEMPOTENT 100%** | Trả về HTTP `200 OK` kèm `ProductDto` nguyên bản (`ProductService.cs:3122-3134`). Auction có thể retry an toàn khi gặp sự cố mạng mà không sợ lỗi. |
| **2** | `lock-auction` không mang `auctionId`: Phiên B gọi lock lên sản phẩm đang bị phiên A khóa cũng nhận 200. Catalog có cần `auctionId` để từ chối phiên khác? | ❌ **KHÔNG CẦN `auctionId`** | Catalog giữ nguyên thiết kế: Catalog quản lý vòng đời Sản phẩm, không quản lý Phiên đấu giá. Catalog đã chặn ở bước `GET .../auction-eligibility` (`IsEligible: false` nếu `InAuction`). Việc đảm bảo 1 SP chỉ có 1 phiên mở thuộc về trách nhiệm `AuctionService` (bằng `ProductOpenAuction` / Distributed Lock). |
| **3** | `unlock-auction` gọi khi sản phẩm đã `Auctioned` (đã bán) thì trả lỗi gì? Giữ `PRODUCT_NOT_IN_AUCTION` như cũ? | ⚠️ **CẢNH BÁO: KHÔNG ĐƯỢC COI LÀ "ĐÃ NHẢ"** | Catalog tách riêng lỗi **`400 PRODUCT_ALREADY_AUCTIONED`** (*"Cannot unlock product that has already been auctioned"*). Sản phẩm đã bán KHÔNG BAO GIỜ được tự động coi là đã nhả về `Active`. |
| **4** | Khi sản phẩm bị Admin khóa/cấm (`moderate`) lúc đang `InAuction`, `lock`/`unlock` phản ứng thế nào? | 🛑 **TỪ CHỐI THAO TÁC** | - `lock`: `400 VAL_INVALID_INPUT` (`"Cannot lock product with status 'Banned'."`)<br>- `unlock`: `400 PRODUCT_NOT_IN_AUCTION` (`"Current status is 'Banned'."`)<br>- **Hành động của Auction:** Khi gặp lỗi này, Auction phải tự động **hủy phiên và hoàn lại cọc** cho người tham gia vì sản phẩm đã bị Sàn tịch thu. |

---

## 2. Chi tiết phân tích kỹ thuật từng câu hỏi

### 2.1. Về tính Idempotent của `POST /api/v1/internal/products/{productId}/mark-auctioned`
Trong mã nguồn CatalogService:
```csharp
if (product.Status == ProductStatus.Auctioned)
{
    // Idempotent: already marked as auctioned
    return new ProductDto(
        product.Id,
        product.SellerId,
        product.CategoryId,
        product.Name,
        product.Description,
        product.Brand,
        product.Status.ToString(),
        product.WeightGrams);
}
```
- **Hành vi:** Khi sản phẩm đã ở trạng thái `Auctioned`, mọi lượt gọi tiếp theo đều trả về `200 OK` và dữ liệu sản phẩm. Hoàn toàn không throw exception.
- **Bộ 4 API đấu giá của Catalog hiện đều đạt Idempotent 100%:**
  - `POST /lock-auction`: Trả `200 OK` nếu đã `InAuction`.
  - `POST /unlock-auction`: Trả `200 OK` nếu đã `Active`.
  - `POST /mark-auctioned`: Trả `200 OK` nếu đã `Auctioned`.
  - `POST /revert-auctioned`: Trả `200 OK` nếu đã `Active`.

---

### 2.2. Về việc `lock-auction` không mang `auctionId`
- **Catalog bảo toàn Bounded Context:** Catalog chỉ quản lý State Machine của Sản phẩm:
  $$\text{Draft} \xrightarrow{\text{Approve}} \text{Active} \xrightarrow{\text{lock-auction}} \text{InAuction} \xrightarrow{\text{mark-auctioned}} \text{Auctioned}$$
- **Quy trình chuẩn liên service:**
  1. Trước khi mở bất kỳ phiên nào, `AuctionService` **BẮT BUỘC** gọi `GET /api/v1/internal/products/{id}/auction-eligibility`.
  2. Nếu sản phẩm đang nằm trong phiên A (`Status == InAuction`), Catalog trả về:
     ```json
     {
       "productId": "...",
       "isEligible": false,
       "reason": "Product is already locked in an ongoing auction.",
       "status": "InAuction"
     }
     ```
     Phiên B sẽ bị từ chối ngay lập tức từ bước kiểm tra điều kiện.
  3. `AuctionService` là chủ sở hữu của Bidding Engine, do đó cơ chế chặn 2 phiên song song cho cùng 1 sản phẩm (bằng bảng `ProductOpenAuction` / Redis lock) ở phía Auction là hoàn toàn đúng kiến trúc vi dịch vụ. Catalog không can thiệp sâu vào session của Auction.

---

### 2.3. Về việc gọi `unlock-auction` trên sản phẩm đã chốt bán (`Auctioned`)
- **Ngộ nhận nguy hiểm của Auction:** Trong tài liệu có ghi: *"Auction coi lỗi này (`PRODUCT_NOT_IN_AUCTION`) là 'đã nhả rồi'"*.
  - **Thực tế:** Nếu sản phẩm đang là `Active` $\to$ Đúng là đã nhả rồi và `unlock-auction` trả về `200 OK`.
  - Nhưng nếu sản phẩm đã bán thành công (`Auctioned`) hoặc bị cấm (`Banned`) $\to$ Sản phẩm **CHƯA HỀ ĐƯỢC NHẢ VỀ ACTIVE**, nó vẫn đang nằm ở trạng thái kết thúc trong CSDL.
  - Nếu Auction tự động coi là "đã nhả" để cho phép mở phiên mới hoặc gán logic sai lệch thì sẽ dẫn tới bất đồng bộ dữ liệu nghiêm trọng.
- **Cập nhật từ Catalog:** Catalog bổ sung mã lỗi riêng:
  ```csharp
  if (product.Status == ProductStatus.Auctioned)
  {
      throw new BusinessRuleValidationException("PRODUCT_ALREADY_AUCTIONED", "Cannot unlock product that has already been auctioned.");
  }
  ```
  Khi Auction nhận mã `PRODUCT_ALREADY_AUCTIONED`, Auction phải hiểu là đơn hàng đã hoàn tất, **không được phép mở lại**. Nếu thực sự cần mở lại do hủy đơn, phải gọi `POST /api/v1/internal/products/{id}/revert-auctioned`.

---

### 2.4. Về trường hợp biên: Sản phẩm bị Admin khóa/cấm (`moderate`) lúc đang `InAuction`
- Khi Admin sàn ra phán quyết vi phạm (hàng giả, hàng cấm, vi phạm tiêu chuẩn cộng đồng) qua API kiểm duyệt `PATCH /api/v1/admin/products/{id}/moderate`:
  - Trạng thái sản phẩm lập tức chuyển sang `Banned` hoặc `Inactive` (`AdminLocked`).
- **Phản ứng của Catalog khi Auction gọi API sau đó:**
  1. `POST /lock-auction` $\to$ Lỗi `400 VAL_INVALID_INPUT` (`"Cannot lock product with status 'Banned'. Product must be in Active status."`).
  2. `POST /unlock-auction` $\to$ Lỗi `400 PRODUCT_NOT_IN_AUCTION` (`"Current status is 'Banned'."`).
  3. `POST /mark-auctioned` $\to$ Bị chặn ngay với `400 PRODUCT_ALREADY_BANNED` (`"Product has been permanently banned and cannot be marked as auctioned."`).
- **Khuyến nghị xử lý cho Auction:** Khi gặp các lỗi trên trong quá trình thanh toán (kể cả luồng Người về nhì), Auction cần xác định phiên đấu giá bị gián đoạn do yếu tố quản trị sàn $\to$ Hủy phiên đấu giá và kích hoạt quy trình hoàn cọc cho toàn bộ người tham gia.
