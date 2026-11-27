# 02 — HỢP ĐỒNG GIAO TIẾP RESTFUL (CATALOG SERVICE REST CONTRACTS)

> **Trạng thái triển khai (2026-11-27):** ✅ ĐÃ CHUẨN HÓA 100% RESTFUL — Toàn bộ 14 Controllers và 53 REST endpoints đã hoàn thiện và kiểm chứng tự động. Bãi bỏ hoàn toàn gRPC, RabbitMQ, MassTransit và Outbox tables.  
> **Nguồn sự thật:** Mã nguồn `Catalog.sln` (`Catalog.Api/Controllers/Public/*.cs`, `Catalog.Api/Controllers/Internal/*.cs`).  
> **Cập nhật lần cuối:** 2026-11-27.  

---

## 1. NGUYÊN TẮC BẤT BIẾN GIAO TIẾP RESTFUL

1. **Pure DTO — Zero ResultFilter:**
   - 100% Controllers trả về DTO thuần túy (`ProductDto`, `CategoryDto`, `PagedData<T>`) hoặc HTTP Status Codes (`200 OK`, `201 Created`, `204 NoContent`).
   - TUYỆT ĐỐI KHÔNG bọc DTO trong phong bì `ApiResponse<T>` tại tầng CatalogService; ApiGateway chịu trách nhiệm bọc envelope ngoài cùng khi gửi tới Client.
2. **Quy chuẩn Định tuyến (100% Plural Collection Routing):**
   - 100% danh từ số nhiều cho collections: `/api/v1/categories`, `/api/v1/products`, `/api/v1/variants`, `/api/v1/images`, `/api/v1/reviews`.
3. **Quy chuẩn Phân trang tại Database (Pagination Governance):**
   - Toàn bộ thao tác phân trang thực hiện trực tiếp tại Database bằng `.Skip((page - 1) * pageSize).Take(pageSize)` và `.CountAsync()`.
   - Kết quả trả về đóng gói theo cấu trúc `PagedData<T>`:
     ```json
     {
       "items": [ ... ],
       "pagination": {
         "pageIndex": 1,
         "pageSize": 20,
         "totalCount": 150,
         "totalPages": 8,
         "hasPreviousPage": false,
         "hasNextPage": true
       }
     }
     ```
4. **Quy chuẩn Thông báo Lỗi (RFC 9110 ProblemDetails):**
   - Định dạng: `application/problem+json`.
   - `message` / `detail`: 100% Tiếng Anh, chuẩn **Sentence case** (viết hoa chữ cái đầu tiên: `"Category not found with the specified ID."`).
   - `errorCode`: 100% **SCREAMING_SNAKE_CASE** (ví dụ: `PRODUCT_NOT_FOUND`, `VAL_INVALID_INPUT`, `FORBIDDEN`). Cấm tiền tố `ERR_`.
5. **Quy chuẩn Enum Serialization:**
   - 100% Enum trả về trong JSON envelope phải là chuỗi `PascalCase` (`"Active"`, `"Draft"`, `"PendingApproval"`, `"Banned"`), lưu trong Database dạng `integer`.
6. **Bảo vệ quyền sở hữu tài nguyên (Seller Ownership):**
   - Khi tạo hoặc cập nhật sản phẩm, `SellerId` bắt buộc trích xuất an toàn từ `HttpContext.GetUserId()`.

---

## 2. MA TRẬN 14 CONTROLLERS & 53 ACTIONS RESTFUL

### 2.1. Phân hệ Danh mục (`CategoriesController` — `/api/v1/categories`)
* `GET /api/v1/categories`: Danh sách danh mục công khai cho người mua, cấu trúc cây cha - con.
* `GET /api/v1/categories/admin`: Danh mục dạng phẳng đầy đủ dành riêng cho quản trị viên.
* `GET /api/v1/categories/{id}`: Chi tiết một danh mục theo GUID.
* `POST /api/v1/categories`: Tạo mới danh mục (Admin/Staff).
* `PUT /api/v1/categories/{id}` & `PATCH /api/v1/categories/{id}`: Cập nhật thông tin danh mục.
* `DELETE /api/v1/categories/{id}`: Vô hiệu hóa hoặc xóa mềm danh mục (Soft Delete).

### 2.2. Phân hệ Sản phẩm (`ProductsController` — `/api/v1/products`)
* `GET /api/v1/products` & `GET /api/v1/products/search`: Tìm kiếm & lọc sản phẩm sàn (chỉ lấy trạng thái `Active`).
* `GET /api/v1/products/{id}`: Xem chi tiết sản phẩm kèm danh sách biến thể SKU và bộ ảnh.
* `POST /api/v1/products`: Người bán tạo sản phẩm thương mại thường (`SaleType.Regular`).
* `PUT /api/v1/products/{id}` & `PATCH /api/v1/products/{id}`: Cập nhật sản phẩm thuộc sở hữu của shop.
* `PATCH /api/v1/products/{id}/status`: Đổi trạng thái hiển thị sản phẩm (`Active` $\leftrightarrow$ `Inactive`).
* `DELETE /api/v1/products/{id}`: Xóa mềm sản phẩm (chỉ cho phép khi chưa phát sinh đơn hàng).
* `GET /api/v1/products/{id}/images`: Lấy bộ ảnh đính kèm của sản phẩm.
* `POST /api/v1/products/{id}/images`: Tải ảnh đính kèm sản phẩm lên Cloudinary.
* `DELETE /api/v1/products/{id}/images/{imageId}`: Xóa một ảnh đính kèm của sản phẩm.
* `POST /api/v1/products/{id}/variants`: Thêm biến thể SKU mới cho sản phẩm.
* `PUT /api/v1/products/{id}/default-variant`: Đặt biến thể SKU mặc định.

### 2.3. Phân hệ Sản phẩm Đấu giá (`ProductsController` & `SellerProductsController`)
* `POST /api/v1/products/auction`: Người bán tạo sản phẩm dành riêng cho đấu giá (`SaleType.Auction`, tự động sinh 1 SKU duy nhất).
* `GET /api/v1/products/auctions`: Tìm kiếm danh sách sản phẩm đấu giá công khai.
* `GET /api/v1/products/auctions/{id}`: Chi tiết sản phẩm đấu giá.
* `PATCH /api/v1/products/auctions/{id}/status`: Cập nhật trạng thái sản phẩm đấu giá.
* `GET /api/v1/seller/products/auctions`: Danh sách sản phẩm đấu giá thuộc sở hữu của người bán đăng nhập.

### 2.4. Phân hệ Người bán Quản lý Sản phẩm (`SellerProductsController` — `/api/v1/seller/products`)
* `GET /api/v1/seller/products`: Danh sách toàn bộ sản phẩm của gian hàng (hỗ trợ lọc `Draft`, `PendingApproval`, `Active`, `Rejected`).
* `GET /api/v1/seller/products/{id}`: Xem chi tiết sản phẩm người bán.

### 2.5. Phân hệ Biến thể SKU (`VariantsController` — `/api/v1/variants`)
* `GET /api/v1/variants/{id}`: Chi tiết SKU (bao gồm tồn kho ảo, kích thước đóng gói GHN `LengthCm`, `WidthCm`, `HeightCm`, `WeightGrams`).
* `PUT /api/v1/variants/{id}` & `PATCH /api/v1/variants/{id}`: Cập nhật giá, mã SKU, thuộc tính biến thể.
* `PATCH /api/v1/variants/{id}/status`: Đổi trạng thái biến thể SKU (`Active` $\leftrightarrow$ `Inactive`).
* `DELETE /api/v1/variants/{id}`: Xóa mềm biến thể SKU.
* `GET /api/v1/variants/{id}/images`: Danh sách ảnh liên kết với biến thể SKU.
* `POST /api/v1/variants/{id}/images`: Thêm ảnh cho biến thể SKU.
* `DELETE /api/v1/variants/{id}/images/{imageId}`: Xóa ảnh của biến thể SKU.

### 2.6. Phân hệ Hình ảnh Độc lập (`ImagesController` — `/api/v1/images`)
* `PATCH /api/v1/images/{id}`: Đổi thứ tự hiển thị `DisplayOrder` hoặc đặt làm ảnh đại diện `IsPrimary`.
* `DELETE /api/v1/images/{id}`: Xóa hình ảnh khỏi hệ thống lưu trữ Cloudinary và Database.

### 2.7. Phân hệ Quản trị & Kiểm duyệt Sản phẩm (`AdminProductsController` — `/api/v1/admin/products`)
* `GET /api/v1/admin/products`: Danh sách sản phẩm toàn sàn có phân trang & lọc kiểm duyệt.
* `GET /api/v1/admin/products/{id}`: Chi tiết sản phẩm để admin/staff thẩm định.
* `PATCH /api/v1/admin/products/{id}/moderate`: Quyết định kiểm duyệt (`Approved`, `Rejected` kèm lý do từ chối).

### 2.8. Phân hệ Báo cáo Vi phạm (`ProductReportsController` & `AdminProductReportsController`)
* `POST /api/v1/products/{id}/reports`: Người mua/Người bán gửi báo cáo tố cáo sản phẩm vi phạm (bắt buộc kèm ảnh bằng chứng).
* `GET /api/v1/products/reports/mine`: Người dùng xem lịch sử các báo cáo do chính mình gửi.
* `GET /api/v1/admin/reports/products`: Danh sách hàng đợi báo cáo vi phạm chờ xử lý.
* `GET /api/v1/admin/reports/products/{reportId}`: Xem chi tiết báo cáo vi phạm và bằng chứng.
* `PUT /api/v1/admin/reports/products/{reportId}/action`: Xử lý báo cáo (`Dismissed` hoặc `ActionTaken` $\to$ tự động cấm sản phẩm sang `Banned` và phạt điểm uy tín).

### 2.9. Phân hệ Đánh giá & Xếp hạng (`ProductReviewsController` & `ReviewsController`)
* `GET /api/v1/products/{id}/rating-summary`: Thống kê số sao trung bình và số lượng đánh giá theo từng mức 1 - 5 sao.
* `GET /api/v1/products/{id}/reviews`: Danh sách đánh giá công khai của sản phẩm có phân trang.
* `GET /api/v1/products/{id}/reviews/eligibility`: Kiểm tra người dùng đăng nhập có đủ điều kiện đánh giá sản phẩm hay không (đã mua đơn thành công và chưa đánh giá).
* `GET /api/v1/products/{id}/reviews/mine`: Lấy đánh giá của chính người dùng cho sản phẩm này.
* `POST /api/v1/products/{id}/reviews`: Gửi đánh giá sản phẩm (1 - 5 sao, nội dung và tối đa 5 ảnh).
* `PUT /api/v1/reviews/{id}`: Người mua chỉnh sửa nội dung đánh giá của mình.
* `DELETE /api/v1/reviews/{id}`: Người mua xóa đánh giá của mình.

### 2.10. Phân hệ Quản trị Kiểm duyệt Đánh giá (`AdminReviewsController` — `/api/v1/admin/reviews`)
* `GET /api/v1/admin/reviews`: Quản trị viên tra cứu toàn bộ đánh giá trên sàn.
* `PATCH /api/v1/admin/reviews/{id}/moderate`: Ẩn hoặc xóa đánh giá vi phạm thuần phong mỹ tục.

### 2.11. Phân hệ Đồng bộ Đơn mua Nội bộ (`InternalPurchasesController` — `/api/v1/internal/purchases`)
* `POST /api/v1/internal/purchases`: `CommerceService` đồng bộ đơn hàng hoàn tất (`Completed`) sang CatalogService để mở khóa quyền đánh giá cho Buyer.

### 2.12. Phân hệ Liên Service với Đấu giá (`InternalProductsController` — `/api/v1/internal/products`)
* `GET /api/v1/internal/products/{id}`: Lấy thông tin snapshot sản phẩm phục vụ tạo phiên đấu giá.
* `GET /api/v1/internal/products/{id}/auction-eligibility`: Kiểm tra điều kiện mở phiên (`Active`, `SaleType.Auction`, không bị khóa).
* `POST /api/v1/internal/products/{id}/lock-auction`: Khóa sản phẩm khi phiên đấu giá bắt đầu mở (`Status` $\to$ `InAuction`).
* `POST /api/v1/internal/products/{id}/unlock-auction`: Mở khóa trả lại trạng thái sản phẩm khi phiên đấu giá bị hủy/kết thúc không người mua.
* `POST /api/v1/internal/products/{id}/mark-auctioned`: Đánh dấu sản phẩm đã đấu giá thành công (`Status` $\to$ `Sold`).
* `POST /api/v1/internal/products/{id}/revert-auctioned`: Hoàn tác trạng thái sản phẩm đấu giá khi người thắng không thanh toán.

### 2.13. Phân hệ Liên Service với Giỏ hàng & Đơn hàng (`InternalVariantsController` — `/api/v1/internal/variants`)
* `GET /api/v1/internal/variants/{id}`: Lấy chi tiết SKU phục vụ Commerce/Fulfillment.
* `POST /api/v1/internal/variants`: Batch lấy thông tin nhiều SKU cùng lúc cho Giỏ hàng (`Cart`).
* `POST /api/v1/internal/variants/check-status`: Kiểm tra trạng thái SKU trước khi tạo đơn hàng (`Checkout`).
* `POST /api/v1/internal/variants/validate-switch`: Xác thực biến thể khi người mua đổi SKU.
* `POST /api/v1/internal/variants/mark-ordered`: Đánh dấu trừ tồn kho ảo khi đơn hàng được tạo.

### 2.14. Phân hệ Đồng bộ Nhật ký Kiểm toán Nội bộ (`InternalAuditLogsController` — `/api/v1/internal/audit-logs`)
* `GET /api/v1/internal/audit-logs/unsynced`: Trả về danh sách nhật ký kiểm toán chưa đồng bộ để dịch vụ Logs trung tâm kéo về.
* `POST /api/v1/internal/audit-logs/mark-synced`: Đánh dấu các bản ghi nhật ký kiểm toán đã được đồng bộ thành công.
