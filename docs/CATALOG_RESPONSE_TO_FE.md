# Phản hồi Kỹ thuật & Bàn giao API cho Frontend Team (CatalogService)

> **Dự án:** Hệ thống Đấu giá & Thương mại điện tử đa dịch vụ (Nexus)  
> **Dịch vụ:** CatalogService (Port `8002` / `5272`, Database `category_db:5434`)  
> **Người gửi:** Catalog Service Backend Engineering Team  
> **Người nhận:** Frontend Team (Web / Mobile) & Core Architecture Team  
> **Ngày cập nhật:** 19/11/2026  
> **Trạng thái:** ✅ Đã hoàn tất triển khai & vượt qua 100% kiểm thử tự động (38/38 tests passed)

---

## 1. Tóm tắt & Định vị Ranh giới Nghiệp vụ (Bounded Context)

Chào team Frontend, Catalog Backend đã đọc kỹ toàn bộ file yêu cầu `CatalogService.md` của các bạn cùng các tài liệu handoff. Sau khi rà soát toàn bộ source code `CatalogService_Refactor`, chúng tôi xin phản hồi chi tiết về các điểm cần thay đổi, các điểm **giữ nguyên (invariants)** và các logic phối hợp đa dịch vụ.

### 📌 Nguyên tắc ranh giới bất biến (Service Boundaries)
1. **CatalogService sở hữu**:
   - Quản lý danh mục (Category đa cấp).
   - Thông tin sản phẩm (Product metadata, description, brand, specifications, status).
   - Phân loại sản phẩm (ProductVariant/SKU, giá bán lẻ, kích thước đóng gói, trọng lượng vận chuyển).
   - Hình ảnh sản phẩm (Cloudinary upload/delete, phân quyền variant).
   - Đánh giá sản phẩm (Product Reviews & Ratings).
   - Báo cáo vi phạm sản phẩm (Product Violation Reports & Moderation).
2. **CatalogService KHÔNG sở hữu (Các service khác phụ trách)**:
   - **Tồn kho vật lý & Cảnh báo tồn kho (`low-stock-alerts`)**: Thuộc về **FulfillmentService** (đã lập văn bản chuyển giao sang Fulfillment).
   - **Giỏ hàng, Đặt hàng, Khuyến mãi (Voucher), Thanh toán**: Thuộc về **CommerceService**.
   - **Phiên đấu giá, Đặt giá (Bid), Tiền cọc (Deposit), Đồng hồ đếm ngược**: Thuộc về **AuctionService**.
   - **Gửi Email / Push Notification**: Thuộc về **NotificationService**.

---

## 2. Chi tiết Giải quyết & Phản hồi Danh mục Tính năng (P0 & P1)

### 🔴 NHÓM P0: TÍNH NĂNG KHẨN CẤP / CHẶN LUỒNG (CRITICAL)

#### 1. API Lấy Chi tiết Sản phẩm Dành cho Internal Service (P0 - Ý 1)
- **Hiện trạng trước đây**: `InternalProductsController` chỉ có endpoint kiểm tra hợp lệ đấu giá (`/auction-eligibility`) và lock/unlock. Khi AuctionService hoặc OrderService gọi endpoint công khai `/api/v1/catalog/products/{id}`, nếu sản phẩm đang ở trạng thái `Draft`, `PendingApproval`, `InAuction` hoặc `Inactive`, API trả về `404 Not Found`.
- **Giải pháp đã thực hiện**:
  - Bổ sung endpoint nội bộ dành riêng cho liên dịch vụ (bỏ qua filter trạng thái công khai):
    - `GET /api/v1/internal/products/{id:guid}`
    - Alias: `GET /api/v1/internal/catalog/products/{id:guid}`
  - Trả về đầy đủ thông tin: `ProductDetailDto` (bao gồm toàn bộ variants, images, specifications, shipping dimensions `LengthCm`, `WidthCm`, `HeightCm`, `WeightGrams`).

#### 2. Đồng bộ Cờ Đã có Đơn hàng (`HasOrder = true`) (P0 - Ý 2)
- **Mục đích**: Bảo vệ tính toàn vẹn dữ liệu kế toán và lịch sử giao dịch. Một SKU đã phát sinh đơn hàng thì Seller **không được phép xóa vĩnh viễn** mà chỉ được chuyển sang trạng thái `INACTIVE` (lý do: `OUT_OF_STOCK` hoặc `DISCONTINUED`).
- **Giải pháp đã thực hiện**:
  - Bổ sung endpoint nội bộ có tính **Idempotent**:
    - `POST /api/v1/internal/variants/mark-ordered`
    - Alias: `POST /api/v1/internal/catalog/skus/mark-ordered`
  - Body: `{ "variantIds": ["guid-1", "guid-2"] }`
  - Response: `200 OK` với `{ "updatedCount": 1, "updatedVariantIds": ["guid-1"] }`. Nếu gọi lại lần 2, hệ thống an toàn không lỗi và trả về `updatedCount: 0`.

#### 3. Giới hạn Phân quyền Admin vs. Seller đối với Nội dung Sản phẩm (P0 - Ý 3)
- **Câu hỏi của FE**: *Admin và Staff có nên sửa tên, mô tả, giá bán, hình ảnh của Seller không?*
- **Quyết định nghiệp vụ thống nhất**: **KHÔNG ĐƯỢC PHÉP**.
  - **Lý do pháp lý & kiểm toán**: Sản phẩm do Seller đăng bán là tài sản và cam kết giao dịch của Seller với người mua. Nếu Admin tự ý sửa giá từ 10.000.000đ thành 1.000.000đ hoặc đổi mô tả sản phẩm, khi xảy ra khiếu nại/tranh chấp, nền tảng không thể xác định trách nhiệm thuộc về ai.
  - **Phân định quyền rõ ràng**:
    - **Seller Role**: Có toàn quyền tạo (`POST`), chỉnh sửa thông tin (`PUT/PATCH`), thêm variant, upload/xóa ảnh sản phẩm của chính mình.
    - **Admin/Staff Role**: Có quyền duyệt duyệt/từ chối (`APPROVE`, `REJECT`), tạm khóa (`SUSPEND`), mở khóa (`UNLOCK`), cấm vĩnh viễn (`BAN`) thông qua `/api/v1/admin/products/{id}/moderate`, xóa cưỡng chế (`DELETE /api/v1/products/{id}`), và xem chi tiết quản trị (`GET /api/v1/admin/products/{id}`).
  - **Điều chỉnh code**: Các API chỉnh sửa nội dung (`PUT /api/v1/products/{id}`, `POST /api/v1/products/{id}/variants`, `DELETE /api/v1/products/{id}/images/{imageId}`) đã được thắt chặt chỉ cho phép `[RequireRole("seller")]`.
  - **Ngoại lệ hiện có trong code (cập nhật 20/11/2026):** hai route ảnh (`PATCH /api/v1/images/{id}` và `DELETE /api/v1/images/{id}`) và `DELETE /api/v1/variants/{id}` vẫn cho phép thêm `admin` (xóa cưỡng chế sản phẩm `DELETE /api/v1/products/{id}` cũng cho `admin`, như mô tả ở trên). Chưa siết thêm; nếu nhóm muốn bỏ ngoại lệ này cần sửa code riêng.

#### 4. Quy trình Cấm/Khóa Sản phẩm đang trong Phiên Đấu giá (`InAuction`) (P0 - Ý 4)
- **Vấn đề đặt ra**: Nếu sản phẩm đang mở phiên đấu giá (`Status = InAuction`) mà vi phạm nghiêm trọng (hàng giả, lừa đảo), Admin có bấm BAN ngay trong Catalog được không?
- **Quy tắc an toàn hệ thống (In-Auction Guard)**:
  - Nếu CatalogService cho phép Admin unilaterally BAN/SUSPEND một sản phẩm đang `InAuction`, AuctionService sẽ bị mất đồng bộ trạng thái, các User đang tham gia đấu giá bị kẹt tiền cọc (Deposit) trong WalletService mà không được hoàn lại tự động!
  - **Giải pháp bảo vệ**:
    - Khi Admin gọi `PATCH /api/v1/admin/products/{id}/moderate` với action `BAN`, `SUSPEND` hoặc `REJECT` trên sản phẩm có trạng thái `InAuction`, CatalogService sẽ chặn lại và trả lỗi HTTP 400:
      ```json
      {
        "status": 400,
        "code": "AUCTION_FORCE_CANCEL_REQUIRED",
        "message": "Product is currently in an active auction. Please force-cancel the auction session in AuctionService first."
      }
      ```
    - **Luồng thao tác chuẩn cho Admin trên Frontend**:
      1. Bước 1: Admin vào màn hình Quản lý Đấu giá, nhấn nút **"Hủy phiên khẩn cấp" (Force Cancel Auction)** tại `AuctionService`.
      2. Bước 2: `AuctionService` sẽ giải phóng cọc/hoàn tiền cho các bidder qua `WalletService`, đồng thời tự động gọi API nội bộ của Catalog: `POST /api/v1/internal/products/{id}/unlock-auction` để đưa sản phẩm về trạng thái an toàn.
      3. Bước 3: Admin thực hiện **BAN** hoặc **SUSPEND** sản phẩm trên giao diện Catalog. Lúc này CatalogService ghi nhận BAN thành công và tự động đóng các đơn tố cáo vi phạm liên quan.

#### 5. Sửa lỗi IDOR khi Xóa Ảnh Biến thể (P0 - Ý 5)
- **Hiện trạng**: API xóa ảnh biến thể trước đây chỉ nhận `imageId` mà không kiểm tra chéo `variantId`.
- **Giải pháp đã thực hiện**:
  - Cập nhật `DELETE /api/v1/variants/{id:guid}/images/{imageId:guid}`:
  - CatalogService kiểm tra:
    1. Ảnh có tồn tại và thuộc sản phẩm của Seller đang đăng nhập không (nếu không: `403 Forbidden`).
    2. `image.VariantId` có khớp chính xác với `{id}` trên URL không (nếu gửi nhầm ảnh của biến thể khác: ném `VAL_INVALID_INPUT` - *"The specified image does not belong to this variant."*).
    3. Sản phẩm có đang trong phiên đấu giá không (nếu có: cấm xóa ảnh).

---

### 🟡 NHÓM P1: HOÀN THIỆN TÍNH NĂNG UI & QUẢN TRỊ (ENHANCEMENTS)

#### 6. API Chi tiết Sản phẩm Dành cho Admin (P1 - Ý 6)
- **Endpoint mới**: `GET /api/v1/admin/products/{id:guid}`
- **Quyền**: `[RequireRole("admin", "staff")]`
- **Khác biệt so với API Public/Seller**:
  - Không lọc theo trạng thái (`Active`, `Draft`, `PendingApproval`, `Inactive`, `Banned`, `InAuction` đều xem được).
  - Trả về toàn bộ danh sách biến thể (kể cả biến thể đã bị tắt `Inactive`).
  - Trả về đầy đủ lý do từ chối kiểm duyệt gần nhất (`rejectionReason`).
  - Trả về kích thước và khối lượng vận chuyển đầy đủ.

#### 7. Màn hình Quản trị Đánh giá Sản phẩm (P1 - Ý 7)
- **Endpoint**: `GET /api/v1/admin/reviews`
- **Quyền**: `[RequireRole("admin", "staff")]`
- **Query Params hỗ trợ lọc**:
  - `page` (default 1), `pageSize` (default 20, max 100)
  - `status`: `Published` hoặc `Hidden` (giá trị khác không khớp enum và bị bỏ qua)
  - `rating`: Lọc theo số sao từ 1 đến 5
  - `productId`: Xem toàn bộ đánh giá của 1 sản phẩm cụ thể
  - `buyerId`: Xem toàn bộ đánh giá do 1 người mua cụ thể viết
  - **Chưa hỗ trợ (cập nhật 20/11/2026):** `userId`, `hasImages`, `fromDate`, `toDate`. Các tham số này bị bỏ qua, không gây lỗi. Sẽ bổ sung khi FE cần.
  - `sortBy`: `createdAt` hoặc `rating`, `sortDirection`: `asc` hoặc `desc`
- **Kết hợp với API Action sẵn có**: `PATCH /api/v1/admin/reviews/{id}/moderate` (Action: `HIDE` hoặc `PUBLISH`) để ẩn đánh giá xấu độc hoặc khôi phục đánh giá.

#### 8. Chi tiết Báo cáo Vi phạm & Danh sách Tố cáo của Tôi (P1 - Ý 8)
- **Endpoint 1 (Admin chi tiết tố cáo)**: `GET /api/v1/admin/reports/products/{reportId:guid}`
  - Quyền: `[RequireRole("admin", "staff")]`
  - Trả về: Chi tiết đơn tố cáo, lý do vi phạm, mô tả của người mua, danh sách ảnh bằng chứng (trường `EvidenceImages`), thông tin sản phẩm bị tố cáo, và ghi chú xử lý của kiểm duyệt viên.
- **Endpoint 2 (User xem lịch sử tố cáo)**: `GET /api/v1/products/reports/mine`
  - Quyền: `[RequireRole("buyer", "seller")]`
  - Trả về: Danh sách các báo cáo vi phạm mà chính tài khoản đang đăng nhập đã gửi lên hệ thống, kèm trạng thái xử lý (`Pending`, `Investigating`, `Resolved`, `Dismissed`) và ghi chú phản hồi từ ban quản trị.

#### 9. Kích thước Đóng gói (`LengthCm`, `WidthCm`, `HeightCm`) & Công thức Vận chuyển GHN (P1 - Ý 9)
- **Chuẩn hóa công thức GHN**:
  - Theo chuẩn API chính thức của Giao Hàng Nhanh (`POST /shiip/public-api/v2/shipping-order/fee`):
    $$\text{Khối lượng tính cước (gram)} = \max\left(\text{Trọng lượng thực tế (gram)}, \frac{\text{Dài (cm)} \times \text{Rộng (cm)} \times \text{Cao (cm)}}{5000} \times 1000\right)$$
  - *Ví dụ*: Một sản phẩm nặng 200g nhưng đóng hộp cồng kềnh $30 \times 20 \times 15\text{ cm}$:
    $$\text{Khối lượng quy đổi} = \frac{30 \times 20 \times 15}{5000} \times 1000 = 1800\text{ gram}$$
    GHN sẽ tính cước trên **1.800g** chứ không phải 200g.
- **Thay đổi tại CatalogService**:
  - Đã thêm 3 trường `LengthCm`, `WidthCm`, `HeightCm` vào Entity `Product` và `ProductVariant`.
  - Hỗ trợ nhập và cập nhật qua các API tạo/sửa sản phẩm và biến thể.
  - Các API chi tiết sản phẩm, batch variants (`CAT-24`), status check (`CAT-39`) và switch validation (`CAT-47`) đều trả về đầy đủ kích thước này để `FulfillmentService` và `CommerceService` tính cước chính xác, không còn bị fallback về kích thước mặc định ($20 \times 15 \times 10\text{ cm}$).

---

## 3. Tổng hợp Bảng Hợp đồng API dành cho Frontend

| Phương thức | Đường dẫn API | Header / Role | Mục đích sử dụng trên UI |
| :--- | :--- | :--- | :--- |
| **GET** | `/api/v1/admin/products/{id}` | Bearer `admin`, `staff` | Màn hình Admin xem toàn bộ chi tiết sản phẩm để kiểm duyệt |
| **GET** | `/api/v1/admin/reviews` | Bearer `admin`, `staff` | Màn hình Admin danh sách & bộ lọc đánh giá sản phẩm |
| **GET** | `/api/v1/admin/reports/products/{reportId}` | Bearer `admin`, `staff` | Màn hình Admin xem chi tiết 1 đơn báo cáo vi phạm |
| **GET** | `/api/v1/products/reports/mine` | Bearer `buyer`, `seller` | Màn hình Người dùng xem lịch sử các báo cáo vi phạm của mình |
| **DELETE** | `/api/v1/variants/{id}/images/{imageId}` | Bearer `seller` | Seller xóa ảnh của biến thể (có kiểm tra chống IDOR) |
| **POST** | `/api/v1/products` | Bearer `seller` | Seller tạo sản phẩm thường (hỗ trợ nhập dài/rộng/cao) |
| **POST** | `/api/v1/products/auction` | Bearer `seller` | Seller tạo sản phẩm đấu giá (hỗ trợ nhập dài/rộng/cao) |
| **PUT/PATCH**| `/api/v1/products/{id}` | Bearer `seller` | Seller cập nhật thông tin sản phẩm |
| **PATCH** | `/api/v1/admin/products/{id}/moderate` | Bearer `admin`, `staff` | Admin duyệt, từ chối, khóa, mở khóa, hoặc cấm sản phẩm |
| **POST** | `/api/v1/internal/variants/mark-ordered` | Internal Service Key | Commerce gọi sang khi đơn hàng tạo thành công |
| **GET** | `/api/v1/internal/products/{id}` | Internal Service Key | Auction/Commerce gọi sang lấy chi tiết sản phẩm |

---

## 4. Hướng dẫn Tích hợp Luồng Nghiệp vụ (Frontend Workflow Guides)

### A. Luồng Đăng bán Sản phẩm có Kích thước Vận chuyển (Seller)
1. **Giao diện**: Form tạo sản phẩm mới bổ sung các trường:
   - Trọng lượng vận chuyển (`weightGrams` - bắt buộc khi gửi duyệt)
   - Chiều dài gói hàng (`lengthCm` - cm, tùy chọn, số nguyên > 0)
   - Chiều rộng gói hàng (`widthCm` - cm, tùy chọn, số nguyên > 0)
   - Chiều cao gói hàng (`heightCm` - cm, tùy chọn, số nguyên > 0)
2. Nếu sản phẩm có nhiều biến thể có kích thước khác nhau (ví dụ thùng 6 chai vs chai lẻ), Seller có thể nhập kích thước riêng ở từng Biến thể (`CreateVariantRequest`). Nếu biến thể không nhập, hệ thống sẽ tự động thừa kế kích thước từ sản phẩm cha.

### B. Luồng Xử lý Vi phạm Sản phẩm Đang Đấu giá (Admin)
1. Khi Admin bấm nút **"Khóa vi phạm" (BAN)** một sản phẩm đang trong phiên đấu giá:
2. Frontend bắt mã lỗi `AUCTION_FORCE_CANCEL_REQUIRED` từ response:
   - Hiển thị thông báo (Modal/Popup): *"Sản phẩm đang nằm trong phiên đấu giá trực tiếp. Vui lòng chuyển sang màn hình Quản lý Đấu giá và bấm 'Hủy phiên đấu giá khẩn cấp' trước khi khóa sản phẩm."*
   - Cung cấp nút chuyển hướng nhanh sang phiên đấu giá tương ứng tại module Auction.
3. Sau khi phiên đấu giá đã được hủy khẩn cấp, Admin quay lại Catalog và nhấn **BAN** thành công.

### C. Bổ sung theo code hiện tại (cập nhật 20/11/2026)

1. **Trạng thái sản phẩm:** `Draft`, `Active`, `Inactive`, `Auctioned`, `InAuction`, `Banned`, `PendingApproval`. Sản phẩm `Auctioned` bị khóa vĩnh viễn, không kiểm duyệt được (`PRODUCT_ALREADY_AUCTIONED`). Sản phẩm `Banned` không kích hoạt lại được (`PRODUCT_ALREADY_BANNED`).
2. **Báo cáo vi phạm sản phẩm:** `POST /api/v1/products/{id}/reports` dùng `multipart/form-data` (`reason`, `description`, `evidenceFiles`, bắt buộc ít nhất 1 ảnh). Admin xem danh sách ở `GET /api/v1/admin/reports/products` và xử lý ở `PUT /api/v1/admin/reports/products/{id}/action` (`BAN_PRODUCT`, `DISMISS`, `INVESTIGATE`, ghi chú 5 đến 2000 ký tự). Mã lỗi thường gặp: `DUPLICATE_REPORT`.
3. **Hệ quả của `BAN_PRODUCT`:** biến thể chuyển `Inactive`, các báo cáo khác cùng sản phẩm tự đóng, seller bị trừ điểm uy tín (số điểm thật do User trả về) và nhận thông báo `ProductBannedByReport`. **Không cấm được sản phẩm đang `InAuction` bằng đường báo cáo** (cùng mã lỗi `AUCTION_FORCE_CANCEL_REQUIRED` như route `moderate`).
4. **Đánh giá:** tạo và sửa đánh giá dùng `multipart/form-data` (có `reviewImages`). Tham số `orderId` thực chất là **ID sub-order**. `GET .../reviews/eligibility` cần `?orderId=`. Trạng thái đánh giá: `Published`, `Hidden`.
5. **Chưa có trang API chi tiết trong `docs/api/`** cho `GET /products/reports/mine`, `POST /internal/variants/mark-ordered` và `GET /admin/products/{id}`.

---
*Mọi thắc mắc kỹ thuật hoặc yêu cầu test thử nghiệm trên môi trường Dev, xin vui lòng liên hệ Backend Catalog Team qua kênh phối hợp kỹ thuật nội bộ.*

