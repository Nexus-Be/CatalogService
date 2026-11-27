# ĐẶC TẢ QUY TẮC NGHIỆP VỤ LIÊN DỊCH VỤ (CROSS-SERVICE BUSINESS INVARIANTS)
## NHỮNG ĐIỀU ĐƯỢC LÀM & CẤM LÀM TOÀN HỆ THỐNG (DO & DO NOT MATRIX)

> **Tài liệu:** Cross-Service Business Rules & Inter-Service Invariants  
> **Dịch vụ chủ quản:** **CatalogService** (Port `8002`, DB `5434` `category_db`)  
> **Phạm vi áp dụng:** Toàn bộ các dịch vụ trong hệ sinh thái Nexus (`FulfillmentService`, `CommerceService`, `AuctionService`, `UserService`, `NotificationService`, `Gateway`, `Nexus Frontend`)  
> **Cập nhật lần cuối:** 27/11/2026  
> **Trạng thái:** ✅ BAN HÀNH CHÍNH THỨC  

---

## 1. GIẢI ĐÁP BẢN CHẤT KIẾN TRÚC: KHI NÀO SẢN PHẨM MỚI ĐƯỢC TẠO VÀO KHO ĐỂ NHẬP HÀNG?

### ❓ Câu hỏi đặt ra:
> *"Tại sao phải cấm Fulfillment nhập kho cho sản phẩm chưa được Staff duyệt? Ủa mà chưa tạo sản phẩm ban đầu bên kho vào kho thì sao nó nhập được?"*

### 💡 Trả lời & Bản chất kỹ thuật:
**Nhận định của Sếp hoàn toàn chính xác về mặt logic kiến trúc!** 
Một sản phẩm không thể nhập kho nếu bản ghi tồn kho của nó chưa từng được khai báo bên kho. Quy trình chính xác được thiết kế theo 3 giai đoạn như sau:

```
[BƯỚC 1: TẠO BẢN NHÁP]
Seller tạo sản phẩm (Draft)
Khai báo SKU, thuộc tính, kích thước GHN
(CHƯA GỌI SANG KHO - KHO CHƯA HỀ CÓ DỮ LIỆU)
        │
        ▼
[BƯỚC 2: KIỂM DUYỆT SẢN PHẨM]
Seller gửi duyệt (PendingApproval)
Staff/Admin duyệt sản phẩm (Status ➔ ACTIVE)
        │
        ▼
[BƯỚC 3: TỰ ĐỘNG KHAI BÁO SKU SANG KHO (INITIALIZE)]
CatalogService tự động gọi S2S:
POST api/v1/internal/inventories/initialize
➔ Khởi tạo bản ghi tồn kho bên FulfillmentService với Qty = 0
(LÚC NÀY SẢN PHẨM MỚI CHÍNH THỨC XUẤT HIỆN TRONG KHO!)
        │
        ▼
[BƯỚC 4: NHẬP HÀNG THỰC TẾ (INBOUND RECEIPT)]
Seller / Thủ kho tạo phiếu nhập kho bên FulfillmentService
➔ Nhập số lượng hàng thực tế để chính thức mở bán!
```

1. **Khi sản phẩm còn ở trạng thái `Draft` hoặc `PendingApproval`:**
   - Sản phẩm chỉ là bản nháp nội bộ trong cơ sở dữ liệu của `CatalogService`.
   - `CatalogService` **TUYỆT ĐỐI KHÔNG GỌI SANG KHO** ở bước này để tránh sinh rác dữ liệu (tránh trường hợp Seller tạo nháp rồi bỏ xó hoặc sản phẩm bị Admin từ chối duyệt mà kho đã lỡ tạo bản ghi).
   - Do đó, bên `FulfillmentService` **hoàn toàn chưa có bản ghi tồn kho nào cho SKU này**.
2. **Khi Staff/Admin bấm Duyệt (`Approved` $\to$ `Active`):**
   - Đây là thời điểm kích hoạt duy nhất! CatalogService sử dụng `ProductInventoryInitializer` tự động gọi sang `FulfillmentService` qua endpoint nội bộ:
     `POST api/v1/internal/inventories/initialize` với `{ skuId, warehouseId, initialQuantity: 0 }`.
   - Bản ghi SKU được tạo lập thành công bên kho vật lý của Seller với số lượng mặc định bằng **0**.
3. **Sau khi SKU đã có mặt trong kho:**
   - Seller mới được phép vào phân hệ Kho của `FulfillmentService` để tạo **Phiếu nhập kho (Inbound Receipt)** để kiểm đếm và đưa số lượng hàng thực tế vào kho.

---

## 2. MA TRẬN QUY TẮC "ĐƯỢC LÀM" VÀ "CẤM LÀM" THEO TỪNG SERVICE

---

### 📦 2.1. ĐỐI VỚI DỊCH VỤ KHO VẬN (FULFILLMENT SERVICE - PORT 8006)

| Nhóm | Nội dung quy tắc | Giải thích & Ràng buộc kỹ thuật |
| :--- | :--- | :--- |
| ✅ **ĐƯỢC LÀM** | **1. Tiếp nhận khai báo SKU ban đầu với số lượng bằng 0** | Tiếp nhận lệnh `POST api/v1/internal/inventories/initialize` từ `CatalogService` khi sản phẩm chuyển `Active`. Khởi tạo bản ghi tồn kho gắn liền với `warehouseId` của Seller. |
| ✅ **ĐƯỢC LÀM** | **2. Cho phép nhập kho thực tế sau khi SKU đã khởi tạo** | Cho phép Seller tạo phiếu nhập kho (`Inbound Receipt`), kiểm đếm và cộng dồn số lượng thực tế (`availableQuantity`) cho SKU đã có trong kho. |
| ✅ **ĐƯỢC LÀM** | **3. Đọc thông số kích thước và khối lượng từ Catalog** | Sử dụng các trường `WeightGrams`, `LengthCm`, `WidthCm`, `HeightCm` của SKU từ Catalog để tính cước và tạo vận đơn GHN. |
| ❌ **CẤM LÀM** | **1. CẤM tự ý tạo mới SKU hoặc nhập hàng cho SKU chưa được duyệt** | Fulfillment KHÔNG ĐƯỢC tự tạo mới SKU mà không thông qua lệnh khởi tạo từ CatalogService. Mọi yêu cầu nhập hàng cho SKU chưa có trong danh mục đều phải bị từ chối với lỗi `SKU_NOT_INITIALIZED_OR_NOT_FOUND`. |
| ❌ **CẤM LÀM** | **2. CẤM nhập kho hoặc xuất kho cho sản phẩm đã bị CẤM (`Banned`)** | Khi một sản phẩm bị Admin áp dụng chế tài `Banned` hoặc tạm dừng `Inactive`, kho không được phép tạo phiếu nhập hàng mới và không được tạo vận đơn xuất hàng cho các đơn hàng phát sinh sau thời điểm cấm. |
| ❌ **CẤM LÀM** | **3. CẤM yêu cầu CatalogService gửi số lượng tồn kho lúc tạo sản phẩm** | Form tạo sản phẩm ở CatalogService tuyệt đối không chứa trường số lượng tồn kho. Quản lý số lượng tồn kho là nghiệp vụ độc quyền của Fulfillment. |
| ❌ **CẤM LÀM** | **4. CẤM truy cập trực tiếp vào Database của CatalogService** | Cấm truy vấn trực tiếp vào DB `category_db:5434`. Mọi trao đổi dữ liệu bắt buộc qua REST API. |

---

### 🛒 2.2. ĐỐI VỚI DỊCH VỤ ĐƠN HÀNG & GIỎ HÀNG (COMMERCE SERVICE - PORT 8003)

| Nhóm | Nội dung quy tắc | Giải thích & Ràng buộc kỹ thuật |
| :--- | :--- | :--- |
| ✅ **ĐƯỢC LÀM** | **1. Kiểm tra trạng thái SKU trước khi Checkout** | Gọi endpoint nội bộ `POST api/v1/internal/variants/check-status` trước khi cho người mua đặt cọc, thanh toán hoặc tạo đơn hàng. |
| ✅ **ĐƯỢC LÀM** | **2. Đồng bộ giao dịch mua hàng hoàn tất sang Catalog** | Khi đơn hàng chuyển trạng thái `Completed`, CommerceService gọi `POST api/v1/internal/purchases` để cấp quyền đánh giá (Review) cho người mua. |
| ❌ **CẤM LÀM** | **1. CẤM tạo đơn hàng cho sản phẩm không ở trạng thái `Active`** | Không được tạo đơn hàng cho sản phẩm đang ở các trạng thái: `Draft`, `PendingApproval`, `Rejected`, `Inactive`, `Banned`, `InAuction`. |
| ❌ **CẤM LÀM** | **2. CẤM tự ý sửa đổi giá bán hoặc tên biến thể** | CommerceService phải lấy đúng `Price` và `Currency` từ CatalogService tại thời điểm tạo đơn, không được tự động tính toán lại giá cơ sở nếu không có voucher hợp lệ. |
| ❌ **CẤM LÀM** | **3. CẤM ghi đè `PurchaseRecord` nếu đơn hàng chưa hoàn tất** | Không được gửi bản ghi mua hàng sang CatalogService khi đơn hàng còn đang giao (`Shipping`), đang xử lý (`Processing`) hoặc đã bị hủy (`Cancelled`). |

---

### 🔨 2.3. ĐỐI VỚI DỊCH VỤ ĐẤU GIÁ (AUCTION SERVICE - PORT 8005)

| Nhóm | Nội dung quy tắc | Giải thích & Ràng buộc kỹ thuật |
| :--- | :--- | :--- |
| ✅ **ĐƯỢC LÀM** | **1. Khóa sản phẩm vào phiên đấu giá** | Khi phiên bắt đầu, gọi `POST api/v1/internal/products/{id}/lock-auction` để CatalogService chuyển trạng thái sản phẩm sang `InAuction`. |
| ✅ **ĐƯỢC LÀM** | **2. Mở khóa khi đấu giá thất bại hoặc bị hủy** | Khi phiên kết thúc mà không có người trả giá hoặc phiên bị hủy, gọi `POST api/v1/internal/products/{id}/unlock-auction` để đưa sản phẩm trở lại `Active`. |
| ✅ **ĐƯỢC LÀM** | **3. Đánh dấu sản phẩm đã bán sau thanh toán** | Khi người thắng hoàn tất thanh toán cọc/đơn hàng, gọi `POST api/v1/internal/products/{id}/mark-auctioned` để đưa sản phẩm về `Sold`. |
| ❌ **CẤM LÀM** | **1. CẤM sửa đổi thông tin sản phẩm và SKU đang `InAuction`** | Trong suốt thời gian sản phẩm mang trạng thái `InAuction`, sản phẩm và biến thể bị đóng băng hoàn toàn (Read-only). Cấm Seller hoặc Service sửa giá, tên, ảnh, thông số. |
| ❌ **CẤM LÀM** | **2. CẤM cấm sản phẩm (`BAN_PRODUCT`) qua đường tố cáo khi đang đấu giá (INV-01)** | CatalogService từ chối lệnh cấm sản phẩm nếu đang `InAuction` để tránh làm kẹt tiền cọc của người tham gia. Admin bắt buộc phải vào AuctionService hủy phiên khẩn cấp trước. |
| ❌ **CẤM LÀM** | **3. CẤM tạo nhiều hơn 1 SKU cho sản phẩm đấu giá (INV-12)** | Sản phẩm đấu giá là độc bản 1-1. CatalogService chỉ tạo đúng 1 biến thể duy nhất, cấm tạo thêm SKU thứ hai. |

---

### 👤 2.4. ĐỐI VỚI DỊCH VỤ NGƯỜI DÙNG & UY TÍN (USER SERVICE - PORT 8001)

| Nhóm | Nội dung quy tắc | Giải thích & Ràng buộc kỹ thuật |
| :--- | :--- | :--- |
| ✅ **ĐƯỢC LÀM** | **1. Tiếp nhận chế tài phạt điểm Seller khi sản phẩm bị cấm** | Tiếp nhận lệnh `POST api/v1/internal/users/penalties` từ CatalogService để trừ ngay 10 điểm uy tín của Seller khi sản phẩm vi phạm bị cấm vĩnh viễn (`BAN_PRODUCT`). |
| ✅ **ĐƯỢC LÀM** | **2. Tiếp nhận điều chỉnh điểm uy tín từ đánh giá sản phẩm** | Tiếp nhận lệnh `POST api/v1/internal/users/reputation-adjust` từ CatalogService (+1 điểm nếu review 5 sao, -1 điểm nếu 2 sao, -2 điểm nếu 1 sao). |
| ❌ **CẤM LÀM** | **1. CẤM Seller can thiệp vào sản phẩm của Seller khác** | UserService cấp token chứa `sub` (User ID). CatalogService bắt buộc đối chiếu `product.SellerId == currentUserId`. Nếu không khớp, trả về ngay `403 FORBIDDEN`. |
| ❌ **CẤM LÀM** | **2. CẤM phơi bày endpoint nội bộ phạt/thưởng uy tín ra bên ngoài** | Các endpoint điều chỉnh uy tín chỉ cho phép gọi qua mạng nội bộ Docker giữa các microservice, không mở ra ngoài Internet. |

---

### 🔔 2.5. ĐỐI VỚI DỊCH VỤ THÔNG BÁO (NOTIFICATION SERVICE - PORT 8007)

| Nhóm | Nội dung quy tắc | Giải thích & Ràng buộc kỹ thuật |
| :--- | :--- | :--- |
| ✅ **ĐƯỢC LÀM** | **1. Gửi thông báo In-App và Email theo sự kiện sản phẩm** | Nhận yêu cầu từ CatalogService để gửi thông báo duyệt (`PRODUCT_APPROVED`), từ chối (`PRODUCT_REJECTED`), hoặc cấm vĩnh viễn (`PRODUCT_BANNED`) kèm email cảnh báo chính sách cho Seller. |
| ❌ **CẤM LÀM** | **1. CẤM làm gián đoạn luồng nghiệp vụ nếu gửi thông báo thất bại** | Cơ chế gửi thông báo là Non-blocking / Best-effort. Nếu NotificationService bị timeout hoặc lỗi SMTP, CatalogService chỉ ghi log warning và **vẫn hoàn tất giao dịch** duyệt/cấm sản phẩm. |

---

### 🌐 2.6. ĐỐI VỚI API GATEWAY (PORT 8080 / YARP)

| Nhóm | Nội dung quy tắc | Giải thích & Ràng buộc kỹ thuật |
| :--- | :--- | :--- |
| ✅ **ĐƯỢC LÀM** | **1. Định tuyến toàn bộ tài nguyên Catalog về `catalog-cluster`** | Trỏ `/api/v1/categories/**`, `/api/v1/products/**`, `/api/v1/variants/**`, `/api/v1/images/**`, `/api/v1/seller/products/**` về `CatalogService:8002`. |
| ✅ **ĐƯỢC LÀM** | **2. Tách biệt Route theo HTTP Method để tránh lỗi 404** | Cấu hình route ưu tiên (`Order: -1`):<br>- `GET /api/v1/admin/reviews` $\to$ **`CatalogService:8002`** (Kiểm duyệt đánh giá sản phẩm).<br>- `PUT /api/v1/admin/reviews/{id}/hide` $\to$ **`UserService:8001`** (Ẩn đánh giá shop).<br>- `GET /api/v1/me/watchlist` & `/me/deposits` $\to$ **`AuctionService:8005`**. |
| ❌ **CẤM LÀM** | **1. CẤM nuốt hoặc xóa bỏ Header định danh người dùng** | Gateway bắt buộc phải chuyển tiếp các header xác thực: `X-User-Id`, `X-User-Roles`, `traceparent` sang CatalogService. |
| ❌ **CẤM LÀM** | **2. CẤM dùng chung một route cho các chức năng trùng path nhưng khác nghiệp vụ** | Tuyệt đối không trỏ chung catch-all `/api/v1/admin/reviews/{**catch-all}` sang UserService vì UserService không có API `GET`. |

---

### 💻 2.7. ĐỐI VỚI FRONTEND (NEXUS WEB & APPS)

| Nhóm | Nội dung quy tắc | Giải thích & Ràng buộc kỹ thuật |
| :--- | :--- | :--- |
| ✅ **ĐƯỢC LÀM** | **1. Tự do thiết kế giao diện tạo sản phẩm (Wizard hoặc Single-step)** | Frontend có thể cho người dùng điền thông tin 1 bước hoặc chia nhiều bước (Thông tin chung $\to$ Biến thể $\to$ Ảnh $\to$ Gửi duyệt). |
| ✅ **ĐƯỢC LÀM** | **2. Bắt buộc có ít nhất 1 ảnh active trước khi gửi duyệt** | Kiểm tra sản phẩm có ít nhất 1 ảnh đại diện (`sortOrder = 0, isActive = true`) trước khi mở nút "Gửi duyệt". |
| ❌ **CẤM LÀM** | **1. CẤM bắt buộc người dùng nhập số lượng tồn kho khi tạo sản phẩm** | Không được đưa ô nhập tồn kho vào màn hình tạo sản phẩm bản nháp (`Draft`). Tồn kho bằng 0 là hợp lệ khi sản phẩm mới tạo. |
| ❌ **CẤM LÀM** | **2. CẤM chặn nút "Kích hoạt / Đăng bán" nếu tồn kho bằng 0** | Không được validation `quantity > 0` khi bấm kích hoạt. Sản phẩm được duyệt với tồn kho ban đầu = 0, việc nhập hàng diễn ra sau tại phân hệ Kho. |
| ❌ **CẤM LÀM** | **3. CẤM gửi các trường tự động (`saleType`, `sellerId`)** | Backend tự động lấy `sellerId` từ Token và gán mặc định `saleType = Regular`. Frontend không được tự ý gửi 2 trường này trong body `POST /api/v1/products`. |
| ❌ **CẤM LÀM** | **4. CẤM tạo SKU thiếu 4 thông số GHN** | Mọi biến thể bắt buộc phải có `weightGrams > 0`, `lengthCm > 0`, `widthCm > 0`, `heightCm > 0`. Nếu thiếu, giao vận GHN sẽ thất bại. |

---

## 3. TÓM TẮT VÒNG ĐỜI VÀ TRẠNG THÁI KHỞI TẠO TỒN KHO (INVENTORY INIT STATE)

Mỗi biến thể (`ProductVariant`) trong CatalogService đều có trường theo dõi đồng bộ kho: `InventoryInitState`:

```
                 ┌────────────────────────────────┐
                 │       State: Uninitialized     │ (Sản phẩm còn Draft / Chưa duyệt)
                 └───────────────┬────────────────┘
                                 │ Staff phê duyệt sản phẩm (Status ➔ Active)
                                 ▼
                 ┌────────────────────────────────┐
                 │         State: Pending         │ (Đang chờ hoặc đang gọi Fulfillment)
                 └───────────────┬────────────────┘
                                 │
                 ┌───────────────┴───────────────┐
                 │ Thành công                    │ Thất bại (Kho chưa sẵn sàng, Timeout...)
                 ▼                               ▼
  ┌──────────────────────────────┐ ┌──────────────────────────────┐
  │         State: Done          │ │   Worker Retry (Tối đa 10)   │
  │  (Đã tạo SKU = 0 bên Kho)   │ └──────────────┬───────────────┘
  └──────────────────────────────┘                │ Quá 10 lần vẫn lỗi
                                                  ▼
                                   ┌──────────────────────────────┐
                                   │    State: NeedsAttention     │
                                   │ (Seller chưa cấu hình kho)   │
                                   └──────────────────────────────┘
```

* **`Uninitialized`:** Trạng thái ban đầu khi biến thể mới tạo. Chưa gọi sang kho.
* **`Pending`:** Đang trong hàng đợi đồng bộ sang FulfillmentService.
* **`Done`:** Đã khai báo thành công SKU bên kho vật lý với số lượng ban đầu = 0.
* **`NeedsAttention`:** Đã retry quá 10 lần nhưng không thành công (thường do Seller chưa đăng ký địa chỉ kho hàng hợp lệ với FulfillmentService). Worker chạy ngầm sẽ tiếp tục thử lại mỗi 6 tiếng.

---

## 4. MA TRẬN MÃ LỖI CHUẨN MỰC LIÊN SERVICE (RFC 9110 SENTENCE CASE)

| Mã lỗi (SCREAMING_SNAKE_CASE) | HTTP Status | Thông báo mẫu (100% English Sentence-case) | Ý nghĩa nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `PRODUCT_NOT_FOUND` | `404 Not Found` | `"Product not found in the system."` | Không tìm thấy sản phẩm theo ID. |
| `PRODUCT_ALREADY_BANNED` | `400 Bad Request` | `"Product has been permanently banned and cannot be modified."` | Sản phẩm đã bị cấm, từ chối mọi thao tác chỉnh sửa hoặc gỡ cấm qua API. |
| `AUCTION_PRODUCT_LOCKED` | `400 Bad Request` | `"Product is currently in auction and cannot be modified."` | Sản phẩm đang đấu giá, từ chối sửa thông tin hoặc xóa biến thể. |
| `CANNOT_BAN_IN_AUCTION` | `400 Bad Request` | `"Cannot ban product while an auction is currently in progress."` | Vi phạm quy tắc INV-01: Không được cấm sản phẩm đang trong phiên đấu giá. |
| `INVALID_PACKAGE_DIMENSIONS` | `400 Bad Request` | `"Package dimensions and weight must be greater than zero for shipping integration."` | Thiếu thông số kích thước/khối lượng phục vụ giao hàng GHN. |
| `ONLY_ONE_VARIANT_ALLOWED` | `400 Bad Request` | `"Auction products are strictly limited to exactly one default variant."` | Vi phạm quy tắc INV-12: Sản phẩm đấu giá chỉ được phép có 1 SKU duy nhất. |
| `CANNOT_REVIEW` | `400 Bad Request` | `"You can only review products that you have successfully purchased and completed."` | Người mua chưa mua hoặc đơn hàng chưa hoàn tất. |
| `REVIEW_WINDOW_EXPIRED` | `400 Bad Request` | `"The 30-day review period for this completed order has expired."` | Đã quá 30 ngày kể từ khi đơn hàng giao thành công. |
| `ALREADY_REVIEWED` | `400 Bad Request` | `"You have already submitted a review for this purchased item."` | Chống trùng lặp đánh giá trên cùng một đơn hàng. |
| `REPORT_EVIDENCE_REQUIRED` | `400 Bad Request` | `"At least one image evidence file is required to submit a report."` | Bắt buộc phải có file ảnh bằng chứng khi gửi tố cáo vi phạm sản phẩm. |
| `FORBIDDEN` | `403 Forbidden` | `"You do not have permission to modify this resource."` | Vi phạm quyền sở hữu tài nguyên (Seller không phải chủ sở hữu sản phẩm). |
