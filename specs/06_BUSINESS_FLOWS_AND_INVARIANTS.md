# 06 — ĐẶC TẢ NGHIỆP VỤ & RÀO CHẮN BẤT BIẾN (CATALOG SERVICE)

> **Dịch vụ:** Product & Inventory Catalog (Port `8002`, DB `5434` `category_db`)  
> **Trạng thái triển khai:** ✅ HOÀN THIỆN — 51 Action REST Controllers, 42 Unit & Contract Tests, Clean Architecture 4 tầng.  
> **Mục đích:** Tài liệu kim chỉ nam giải thích toàn diện vòng đời sản phẩm, các quy tắc nghiệp vụ cốt lõi, ma trận "Được làm vs Cấm làm" (Business Invariants) và các luồng tích hợp liên dịch vụ (Inter-Service Workflows) nhằm phục vụ đội ngũ phát triển và các AI Agent.  
> **Cập nhật lần cuối:** 2026-11-25.  

---

## 1. TỔNG QUAN RANH GIỚI MIỀN (BOUNDED CONTEXT)

**CatalogService** là trung tâm quản lý danh mục và sản phẩm của nền tảng thương mại điện tử & đấu giá Nexus:
1. **Quản lý Danh mục (Categories):** Cây phân cấp đa tầng (`ParentId`, `Level`, `SortOrder`), hỗ trợ ẩn/hiện danh mục (`IsDisabled`).
2. **Quản lý Sản phẩm (Products):** Tên, mô tả, thương hiệu, loại bán (`SaleType.Regular` hoặc `SaleType.Auction`), thông số kỹ thuật (`Specifications` dạng jsonb), trạng thái kiểm duyệt và kinh doanh.
3. **Quản lý Biến thể (Product Variants / SKUs):** Thuộc tính (`Color`, `Size`), giá bán (`Price`, `Currency`), mã SKU, khối lượng (`WeightGrams`), kích thước đóng gói GHN (`LengthCm`, `WidthCm`, `HeightCm`).
4. **Quản lý Đánh giá & Xếp hạng (Product Reviews & Ratings):** Tiếp nhận đánh giá 1–5 sao kèm tối đa 5 ảnh đính kèm từ người mua đã hoàn tất đơn hàng, tự động tính điểm sao trung bình và tác động uy tín người bán.
5. **Quản lý Tố cáo Sản phẩm (Product Reports):** Tiếp nhận khiếu nại/tố cáo hàng giả, hàng cấm từ Buyer/Seller kèm bằng chứng ảnh bắt buộc; cung cấp giao diện kiểm duyệt cho Admin/Staff với chế tài cấm sản phẩm và phạt điểm uy tín.
6. **Kho ảo & Đồng bộ Tồn kho (Virtual Inventory & Fulfillment Handoff):** Quản lý tồn kho ảo khả dụng để bán, tự động khai báo và đồng bộ định danh SKU sang kho vật lý của `FulfillmentService`.

---

## 2. VÒNG ĐỜI SẢN PHẨM (PRODUCT LIFECYCLE STATE MACHINE)

```
                       ┌──────────────┐
                       │    Draft     │
                       └──────┬───────┘
                              │ Seller gửi duyệt (Submit for Approval)
                              ▼
                       ┌──────────────┐
            ┌─────────►│PendingApproval│◄────────┐
            │          └──────┬───────┘         │
            │                 │                 │
Admin từ chối (Reject)        │ Admin duyệt     │ Seller sửa lại (Resubmit)
            │                 │ (Approve)       │
            ▼                 ▼                 │
      ┌───────────┐    ┌──────────────┐         │
      │ Rejected  ├───►│    Active    ├─────────┘
      └───────────┘    └──────┬───────┘
                              │
          ┌───────────────────┼───────────────────┐
          │ Tạm tắt/bật       │ Tham gia đấu giá  │ Vi phạm chính sách
          ▼                   ▼                   ▼
    ┌───────────┐      ┌─────────────┐     ┌───────────┐
    │ Inactive  │      │  InAuction  │     │  Banned   │ (Vĩnh viễn)
    └───────────┘      └──────┬──────┘     └───────────┘
                              │ Đấu giá kết thúc (Handoff Order)
                              ▼
                       ┌─────────────┐
                       │    Sold     │
                       └─────────────┘
```

### Các trạng thái chi tiết:
* **`Draft`:** Sản phẩm mới tạo bởi Seller, chưa hoàn thiện thông tin biến thể/ảnh, chưa xuất hiện trên sàn.
* **`PendingApproval`:** Seller đã hoàn tất thông tin và gửi yêu cầu phê duyệt lên ban quản trị.
* **`Active`:** Sản phẩm đã được Admin/Staff phê duyệt hợp lệ. Sản phẩm và các SKU hiển thị công khai trên sàn cho khách hàng tìm kiếm và mua sắm.
* **`Rejected`:** Bị Admin từ chối do thông tin sai lệch hoặc vi phạm chính sách đăng tải nhẹ (kèm lý do `StatusReason`). Seller có quyền chỉnh sửa và gửi duyệt lại.
* **`Inactive`:** Seller chủ động tạm dừng bán, hoặc hết hàng, hoặc Admin tạm khóa kiểm tra. Có thể kích hoạt lại `Active`.
* **`InAuction`:** Sản phẩm đấu giá đang trong phiên đấu giá trực tiếp do `AuctionService` quản lý. Toàn bộ thông tin sản phẩm và biến thể bị **đóng băng (Read-only)**.
* **`Sold`:** Sản phẩm (đặc biệt là sản phẩm đấu giá độc bản 1-1) đã có người thắng và chuyển sang giai đoạn tạo đơn hàng bên `CommerceService`.
* **`Banned`:** Bị Admin cấm vĩnh viễn do hàng giả, hàng cấm, lừa đảo (thông qua phán quyết đơn tố cáo hoặc kiểm duyệt thủ công). Trạng thái này là **Terminal State** (không thể phục hồi).

---

## 3. MA TRẬN RÀO CHẮN NGHIỆP VỤ BẤT BIẾN (INVARIANTS: ĐƯỢC LÀM VS CẤM LÀM)

Bất kỳ AI Agent hoặc Developer nào làm việc tại CatalogService **BẮT BUỘC TUÂN THỦ** các quy tắc bất biến sau:

| Mã quy tắc | Rào chắn nghiệp vụ | Bằng chứng mã nguồn | Ý nghĩa & Hậu quả nếu vi phạm |
| :--- | :--- | :--- | :--- |
| **INV-01 (Rule D-13)** | ❌ **CẤM cấm sản phẩm (`BAN_PRODUCT`) qua đường tố cáo nếu sản phẩm đang trong phiên đấu giá (`InAuction`).** | `ProductReportService.cs:340-348` | Nếu sản phẩm đang diễn ra đấu giá mà bị cấm đột ngột, cọc của người tham gia và luồng settlement bên `AuctionService` sẽ bị treo không thể hoàn tiền. Bắt buộc Admin phải dùng quyền khẩn cấp hủy phiên đấu giá bên `AuctionService` trước. |
| **INV-02** | ❌ **CẤM xóa cứng (Hard Delete) sản phẩm hoặc biến thể.** | `ProductService.cs`, `VariantsController.cs` | Bắt buộc áp dụng Soft-delete (`IsDeleted = true`). Việc xóa cứng sẽ phá vỡ toàn vẹn dữ liệu đơn hàng lịch sử bên `CommerceService` và hóa đơn GHN bên `FulfillmentService`. |
| **INV-03** | ❌ **CẤM khách hàng đánh giá sản phẩm khi chưa mua hàng hoặc chưa hoàn thành đơn.** | `ProductReviewService.cs:170-220` | Đánh giá chỉ hợp lệ khi tìm thấy bản ghi mua hàng (`PurchaseRecord`) từ Commerce với trạng thái `Completed`. Người dùng chưa mua hoặc đơn đang giao (`Shipping`) sẽ bị từ chối `400 CANNOT_REVIEW`. |
| **INV-04** | ❌ **CẤM đánh giá sau quá 30 ngày kể từ khi đơn hàng hoàn tất.** | `ProductReviewService.cs:205` (`MaxReviewPeriodDays = 30`) | Quá 30 ngày, hệ thống trả về mã lỗi `REVIEW_WINDOW_EXPIRED`. Đảm bảo tính thời sự và chân thực của bài đánh giá. |
| **INV-05** | ❌ **CẤM một đơn hàng gửi nhiều hơn một bài đánh giá (Chống trùng lặp - Idempotent).** | `ProductReviewService.cs:188` | Một khách hàng mua cùng 1 sản phẩm ở 1 đơn hàng chỉ được để lại 1 bài review duy nhất. Trả về `ALREADY_REVIEWED` nếu đã đánh giá. |
| **INV-06** | ❌ **CẤM gửi tố cáo vi phạm sản phẩm mà không có file ảnh bằng chứng.** | `ProductReportService.cs:115` | Đơn tố cáo bắt buộc phải có ít nhất 1 ảnh bằng chứng (`evidenceFiles.Count > 0`, tối đa 5 file, định dạng jpg, png, webp). Tránh tình trạng spam tố cáo vu khống đối thủ cạnh tranh. |
| **INV-07** | ✅ **KHI CẤM SẢN PHẨM: Tự động trừ 10 điểm uy tín Seller & đóng hàng đợi tố cáo.** | `ProductReportService.cs:355-385` | Khi chọn hành động `BAN_PRODUCT`: <br>1. Trừ 10 điểm uy tín seller qua `IUserClient.PenalizeSellerAsync`.<br>2. Vô hiệu hóa toàn bộ SKU (`VariantStatus.Inactive`).<br>3. Tự động đóng tất cả các đơn tố cáo khác đang chờ duyệt của sản phẩm này.<br>4. Gửi thông báo In-app cho Seller và Buyer. |
| **INV-08** | ✅ **KHI CÓ REVIEW 1–5 SAO: Tự động điều chỉnh điểm uy tín Seller.** | `ProductReviewService.cs:320-330, 450-460` | Đánh giá 4–5 sao cộng uy tín, 1–2 sao trừ uy tín. CatalogService tự động gọi `IUserClient.AdjustReputationAsync` sang `api/v1/internal/users/reputation-adjust` của `UserService`. |
| **INV-09** | ✅ **KHI DUYỆT SẢN PHẨM: Khởi tạo tồn kho SKU sang Fulfillment bằng cơ chế Non-blocking.** | `ProductInventoryInitializer.cs:150-237` | Khi Admin duyệt sản phẩm, SKU được gán `InventoryInitState.Pending`. Worker nền thử gọi `FulfillmentService`. Nếu seller chưa có kho, ghi warning và retry backoff (tối đa 10 lần), **tuyệt đối không quăng exception làm sập luồng duyệt**. |
| **INV-10** | ❌ **CẤM dùng Repository Pattern bọc ngoài EF Core.** | `specs/00_AGENT_MISSION_GUARDRAILS.md` | Sử dụng trực tiếp `AppDbContext` / `ICatalogDbContext` và `DbSet<T>` trong tầng Application. Cấm tạo `IProductRepository`, `ICategoryRepository`. |
| **INV-11** | ❌ **CẤM tạo SKU thiếu thông số kích thước đóng gói và khối lượng cho đơn vị vận chuyển GHN.** | `ProductVariant.cs:22-26`, `AddVariantRequestValidator.cs` | Bắt buộc khai báo `WeightGrams > 0`, `LengthCm > 0`, `WidthCm > 0`, `HeightCm > 0`. Thiếu các trường này API GHN sẽ từ chối tạo vận đơn và tính phí ship. |
| **INV-12** | ❌ **CẤM sản phẩm đấu giá (`SaleType.Auction`) sở hữu nhiều hơn 1 biến thể SKU.** | `ProductsController.cs:115-130`, `ProductService.cs` | Sản phẩm đấu giá là độc bản 1-1; hệ thống tự động sinh đúng 1 SKU mặc định duy nhất khi người bán đăng tải. |
| **INV-13** | ❌ **CẤM để lỗi đồng bộ nhật ký kiểm toán làm tắc nghẽn luồng nghiệp vụ chính (Non-blocking Resilient Audit).** | `InternalAuditLogsController.cs`, `AuditSyncSourceService.cs` | Việc kéo hoặc đánh dấu nhật ký đã đồng bộ được thực hiện độc lập, không giữ khóa dữ liệu đơn hàng hay sản phẩm. |
| **INV-14** | ❌ **CẤM nhập hàng tồn kho cho sản phẩm chưa được Staff/Admin duyệt.** | `ProductInventoryInitializer.cs`, `docs/CROSS_SERVICE_BUSINESS_RULES_AND_RESTRICTIONS.md` | Sản phẩm còn ở `Draft`/`PendingApproval` chưa hề được khai báo sang kho. Chỉ khi Staff duyệt `Active`, SKU mới được khởi tạo với số lượng ban đầu bằng 0, sau đó mới cho phép tạo phiếu nhập kho. |

> [!NOTE]
> Ma trận chi tiết toàn bộ các quy tắc "Được làm vs Cấm làm" liên dịch vụ (DO & DO NOT Matrix) giữa CatalogService và các dịch vụ khác (`FulfillmentService`, `CommerceService`, `AuctionService`, `UserService`, `NotificationService`, `Gateway`, `Frontend`) được quy định tập trung tại:  
> 👉 [CROSS_SERVICE_BUSINESS_RULES_AND_RESTRICTIONS.md](file:///d:/Apartment/FALL_26/FSA/Project/CatalogService_Refactor/docs/CROSS_SERVICE_BUSINESS_RULES_AND_RESTRICTIONS.md).

---

## 4. CÁC LUỒNG TÍCH HỢP LIÊN DỊCH VỤ (INTER-SERVICE WORKFLOWS)

```
┌────────────────────────────────────────────────────────────────────────┐
│                        CATALOG SERVICE (Port 8002)                     │
└───────┬───────────────────┬───────────────────┬──────────────────┬─────┘
        │                   │                   │                  │
        ▼ S2S REST          ▼ S2S REST          ▼ S2S REST         ▼ S2S REST
┌───────────────┐   ┌───────────────┐   ┌───────────────┐   ┌──────────────┐
│  UserService  │   │NotificationSvc│   │FulfillmentSvc │   │  CommerceSvc │
│  (Port 8001)  │   │  (Port 8007)  │   │  (Port 8006)  │   │  (Port 8003) │
├───────────────┤   ├───────────────┤   ├───────────────┤   ├──────────────┤
│- Phạt uy tín  │   │- Báo In-App   │   │- Tạo kho SKU  │   │- Đồng bộ mua │
│  khi cấm SP   │   │- Gửi Email    │   │  ban đầu      │   │  hàng review │
│- Điều chỉnh   │   │  cảnh báo SP  │   │- Kiểm tra kho │   │- Khóa SKU khi│
│  uy tín review│   │  bị cấm       │   │  Seller       │   │  đã lên đơn  │
└───────────────┘   └───────────────┘   └───────────────┘   └──────────────┘
```

### 4.1. Tích hợp với UserService (Port `8001`)
1. **Phạt điểm uy tín (`POST api/v1/internal/users/penalties`):**
   - Kích hoạt khi: Admin thực hiện `BAN_PRODUCT` trên đơn tố cáo hoặc cấm thủ công sản phẩm.
   - Tham số: `userId: <sellerId>`, `penaltyPoints: 10`, `reason: "PRODUCT_BANNED_REPORT"`, `referenceId: <productId>`.
   - Cơ chế: Resilient non-blocking execution (nếu UserService lỗi, CatalogService ghi warning và vẫn hoàn tất cấm sản phẩm).
2. **Cộng / trừ uy tín theo đánh giá (`POST api/v1/internal/users/reputation-adjust`):**
   - Kích hoạt khi: Người mua đăng bài review mới hoặc sửa rating sao của review cũ.
   - Tham số: `userId: <sellerId>`, `referenceId: <orderId>`, `points: +1` (nếu 5 sao) hoặc `-1` (nếu 1 sao).
   - Cơ chế: Idempotent bằng `ReferenceId` (tránh spam cày điểm trên cùng 1 đơn hàng).

### 4.2. Tích hợp với NotificationService (Port `8007`)
1. **Thông báo thường trong hệ thống (In-App Notification):**
   - Endpoint: `POST api/v1/internal/notifications/in-app`.
   - Sử dụng khi:
     - Gửi Buyer: Xác nhận tiếp nhận đơn tố cáo (`REPORT_SUBMITTED`), thông báo kết quả giải quyết tố cáo (`REPORT_RESOLVED` hoặc `REPORT_DISMISSED`).
     - Gửi Seller: Thông báo sản phẩm được duyệt (`PRODUCT_APPROVED`), bị từ chối duyệt (`PRODUCT_REJECTED`).
2. **Thông báo quan trọng (Gửi cả In-App + Email):**
   - Endpoint Email: `POST api/v1/notifications/email/send`.
   - Sử dụng khi:
     - **Cấm sản phẩm (`BAN_PRODUCT`):** Sản phẩm bị gỡ vĩnh viễn và bị trừ 10 điểm uy tín $\rightarrow$ Gửi email cảnh báo vi phạm bản quyền/chính sách và thông báo chế tài tài khoản đến hộp thư của Seller.
     - **Từ chối duyệt sản phẩm (`REJECT_PRODUCT`):** Gửi email chi tiết lý do từ chối để Seller nắm bắt và chỉnh sửa.

### 4.3. Tích hợp với FulfillmentService (Port `8006`)
1. **Khởi tạo tồn kho ban đầu (`POST api/v1/internal/inventories/initialize`):**
   - Kích hoạt khi: Sản phẩm loại thường (`SaleType.Regular`) được phê duyệt chuyển sang `Active`.
   - Quy tắc kho: Mỗi Seller có duy nhất 1 kho tư nhân hoạt động (`GetWarehousesBySellerIdAsync`).
   - Cơ chế chịu lỗi: Non-blocking Outbox Worker (`ProductInventoryInitializer`). Nếu kho chưa sẵn sàng, retry tăng dần (1, 2, 5, 10, 30 phút), không chặn luồng duyệt của Admin.

### 4.4. Tích hợp với AuctionService (Port `8005`)
1. **Khóa sản phẩm đấu giá (`POST api/v1/internal/products/{id}/lock-auction`):**
   - Đưa sản phẩm sang trạng thái `InAuction`, đóng băng biến thể và giá bán.
2. **Nhả khóa sản phẩm đấu giá (`POST api/v1/internal/products/{id}/unlock-auction`):**
   - Khi phiên đấu giá bị hủy hoặc không có người mua, mở lại trạng thái `Active`.
3. **Đánh dấu đã bán (`POST api/v1/internal/products/{id}/mark-auctioned`):**
   - Khi người thắng thanh toán xong, chuyển sản phẩm sang `Sold`.

---

## 5. CHỈ MỤC TRA CỨU TÀI LIỆU CHI TIẾT (API DIRECTORY MAPPING)

Toàn bộ 53 tài liệu đặc tả endpoint chi tiết được lưu trữ có cấu trúc tại thư mục `docs/api/`:
- **Quản lý Sản phẩm (Products):** `docs/api/Products/` (12 endpoints: CRUD, Moderate, Search, Paging).
- **Quản lý Danh mục (Categories):** `docs/api/Categories/` (6 endpoints: Cây phân cấp, Admin, Sắp xếp).
- **Quản lý Biến thể (Variants/SKUs):** `docs/api/Variants/` (9 endpoints: Thuộc tính, Kích thước GHN, Trọng lượng).
- **Quản lý Hình ảnh (Images):** `docs/api/Images/` (4 endpoints: Upload Cloudinary, Thumbnail, Sắp xếp).
- **Đánh giá Sản phẩm (Reviews):** `docs/api/Reviews/` (7 endpoints: Rating Summary, Đăng đánh giá, Sửa/Xóa, Duyệt).
- **Tố cáo Vi phạm (Reports):** `docs/api/Reports/` (4 endpoints: Gửi tố cáo, Xem báo cáo của tôi, Danh sách chờ, Phán quyết cấm).
- **Sản phẩm Người bán (Seller Products):** `docs/api/SellerProducts/` (3 endpoints: Dashboard của shop, Chi tiết, Đấu giá của shop).
- **Giao tiếp Nội bộ (Internal S2S):** `docs/api/Internal/` (12 endpoints: Lock Auction, Mark Ordered, Check Status, Sync Purchases, Audit Logs).

---

## 6. MA TRẬN 7 GIAI ĐOẠN KIỂM THỬ TÍCH HỢP TOÀN HỆ THỐNG (E2E BUSINESS FLOW)

Luồng kiểm thử thực tế được thực hiện tuần tự để xác minh toàn vẹn tính năng nghiệp vụ liên dịch vụ:

| Giai đoạn | Bước thực hiện | Tác nhân | Màn hình / Endpoint | Kết quả kỳ vọng & Ràng buộc CatalogService |
| :--- | :--- | :--- | :--- | :--- |
| **G1: Auth & Onboarding** | **1.1 Đăng nhập & Đăng ký** | Người dùng | `/login`, `/register` | Xác thực JWT Token, nhận vai trò `buyer` / `seller` / `staff` / `admin`. |
| | **1.2 Đăng ký hồ sơ Người bán** | Người bán | `/register` (Bước 2) | Khai báo địa chỉ kho hàng phục vụ tích hợp vận chuyển GHN. |
| **G2: Categories** | **2.1 Duyệt cây danh mục** | Mọi người | `GET /api/v1/categories` | Trả về cây danh mục phân tầng cha - con (`Children`). |
| | **2.2 Quản trị danh mục** | Admin/Staff | `POST/PUT /api/v1/categories` | Tạo mới/sửa ngành hàng, tự động chuẩn hóa cấp bậc (`Level`). |
| **G3: Product Creation & Approval** | **3.1 Tạo sản phẩm thường** | Người bán | `POST /api/v1/products` | Trạng thái ban đầu `Draft`, khai báo thông số và ảnh Cloudinary. |
| | **3.2 Cấu hình SKU & Kích thước GHN** | Người bán | `POST /api/v1/products/{id}/variants` | Khai báo `LengthCm`, `WidthCm`, `HeightCm`, `WeightGrams` (> 0). |
| | **3.3 Gửi duyệt & Phê duyệt** | Seller $\to$ Staff | `PATCH /api/v1/admin/products/{id}/moderate` | Chuyển `PendingApproval` $\to$ `Active`. Kích hoạt Non-blocking Outbox đồng bộ SKU sang Fulfillment. |
| **G4: Inventory & Reporting** | **4.1 Quản lý tồn kho ảo** | Người bán | `GET /api/v1/variants/{id}` | Quản lý `AvailableQuantity` và `ReservedQuantity`. |
| | **4.2 Người mua gửi tố cáo vi phạm** | Người mua | `POST /api/v1/products/{id}/reports` | Bắt buộc đính kèm tối đa 5 file ảnh bằng chứng. |
| | **4.3 Xử lý tố cáo & Phạt shop** | Admin/Staff | `PUT /api/v1/admin/reports/products/{id}/action` | Chọn `ActionTaken` $\to$ Sản phẩm chuyển `Banned` vĩnh viễn, vô hiệu hóa SKU, trừ 10 điểm uy tín Seller qua UserService. |
| **G5: Purchasing Flow** | **5.1 Bỏ giỏ & Thanh toán** | Người mua | `/cart`, `/checkout` | `POST /api/v1/internal/variants/check-status` kiểm tra tính hợp lệ trước khi đặt cọc/thanh toán. |
| **G6: Auction Lifecycle** | **6.1 Tạo sản phẩm đấu giá** | Người bán | `POST /api/v1/products/auction` | Tạo sản phẩm `SaleType.Auction`, tự động sinh đúng 1 SKU mặc định. |
| | **6.2 Mở phiên & Đóng băng SP** | Hệ thống | `POST /api/v1/internal/products/{id}/lock-auction` | `Status` $\to$ `InAuction`. Cấm sửa/xóa sản phẩm và biến thể. |
| | **6.3 Kết thúc đấu giá & Bàn giao** | Hệ thống | `POST /api/v1/internal/products/{id}/mark-auctioned` | Chuyển `Status` $\to$ `Sold`, tạo đơn hàng tương ứng bên Commerce. |
| **G7: Review & Reputation Impact** | **7.1 Đồng bộ đơn mua hoàn tất** | Hệ thống | `POST /api/v1/internal/purchases` | `CommerceService` đồng bộ đơn `Completed` sang `PurchaseRecord` của Catalog. |
| | **7.2 Người mua gửi đánh giá** | Người mua | `POST /api/v1/products/{id}/reviews` | Kiểm tra quyền đánh giá hợp lệ, chấm 1 - 5 sao kèm tối đa 5 ảnh. |
| | **7.3 Tác động điểm uy tín Seller** | Hệ thống | `POST /api/v1/internal/users/reputation-adjust` | Tự động trừ điểm: 1 sao = -2 điểm, 2 sao = -1 điểm, 5 sao = +1 điểm. |
| | **7.4 Đồng bộ nhật ký kiểm toán** | Dịch vụ Logs | `GET /api/v1/internal/audit-logs/unsynced` | Kéo nhật ký kiểm toán và xác nhận bằng `POST mark-synced`. |

