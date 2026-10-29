# NHẬT KÝ CÔNG VIỆC (TASK LOG) — CATALOG SERVICE

---

## 📅 Ngày: 27/10/2026

### 1. Cấu hình & Kiểm tra Database (Neon PostgreSQL)
- **Cấu hình:** Đồng bộ chuỗi kết nối Neon Cloud (`catalog_db`) vào file thật `appsettings.json` và `.env`.
- **Kiểm tra kết nối:** Xác nhận kết nối thành công tới database trên Neon (đã có sẵn 12 bảng nghiệp vụ và 5 sản phẩm mẫu đang `Active`).

### 2. Khắc phục lỗi vòng lặp Migration & Khởi động
- **Lỗi Migration:** Code local mang migration `20261024103127_InitialCatalogSchema` nhưng bảng `__EFMigrationsHistory` trên Neon lại ghi nhận `20261026114653_Initial` → EF Core cố tạo lại bảng `AuditLogs` đã có gây lỗi lặp.
- **Xử lý:** Đồng bộ chèn record migration vào `__EFMigrationsHistory` trên Neon DB → Database đồng bộ sạch sẽ, không còn lỗi `relation "AuditLogs" already exists`.

### 3. Tối ưu Message Bus (MassTransit / RabbitMQ) cho môi trường Local
- **Vấn đề:** Khi chạy local không bật RabbitMQ container, MassTransit liên tục retry kết nối làm spam log và nghẽn ứng dụng.
- **Xử lý:** Cập nhật `Catalog.Infrastructure/DependencyInjection.cs`:
  - Môi trường `Development` (chạy local): Tự động chuyển sang dùng **In-Memory Transport** (`loopback://`).
  - Môi trường `Staging / Production`: Giữ nguyên kết nối **RabbitMQ thật** + **EF Core Transactional Outbox**.

### 4. Hoàn thiện tính năng Lấy danh sách sản phẩm (CAT-06)
- **Tầng Application (`ProductDtos.cs` & `ProductService.cs`):**
  - Tạo `ProductListItemDto` tối ưu dữ liệu hiển thị (ảnh đại diện, khoảng giá min-max, đánh giá sao).
  - Tạo `ProductListQuery` gom các bộ lọc tìm kiếm (`keyword`, `categoryId`, `sellerId`, `saleType`, `sortBy`, `page`, `pageSize`).
  - Tạo Generic container `PagedList<T>` hỗ trợ phân trang tái sử dụng đa module.
  - Hoàn thiện logic `SearchAsync` truy vấn trực tiếp qua `ICatalogDbContext` (No-Repository pattern, `.AsNoTracking()`).
- **Tầng API Controller (`ProductsController.cs`):**
  - Xóa bỏ constructor 2 tham số cũ, chỉ giữ 1 constructor duy nhất 3 tham số để tránh lỗi DI container.
  - Đồng bộ Swagger schema: Cập nhật `[ProducesResponseType]` trỏ về `ProductListItemDtoPagedDataApiResponse`.

### 5. Cập nhật công cụ & Kiểm thử thực tế
- Cập nhật host port trong `Catalog.Api.http` về đúng chuẩn port local: `@Host = http://localhost:5272`.
- **Build Solution:** `0 Warning(s)`, `0 Error(s)`.
- **Test Endpoint:** Gọi thực tế `GET /api/v1/catalog/products` thành công trả về HTTP `200 OK` với đầy đủ 5 sản phẩm mẫu từ database Neon.

### 6. Hoàn thiện tính năng Xem chi tiết 1 sản phẩm (CAT-05)
- **Tầng Application (`ProductDtos.cs` & `ProductService.cs`):**
  - Khai báo DTO chi tiết: `ProductDetailDto` lồng các DTO con (`ProductDetailVariantDto`, `ProductDetailImageDto`, `ProductDetailCategoryDto`, `ProductDetailRatingDto`).
  - Khai báo `GetDetailAsync` trong `IProductService`.
  - Cài đặt `GetDetailAsync` trong `ProductService.cs` truy vấn trực tiếp qua `ICatalogDbContext` (No-Repository, `.Select()` projection tự động map Variants, Images, Category, RatingStats).
- **Tầng API Controller (`ProductsController.cs`):**
  - Cập nhật `GetProduct` gọi `_productService.GetDetailAsync`.
  - Đồng bộ Swagger schema response `ProductDetailDto`.
- **Kiểm thử thực tế:** Gọi endpoint `GET /api/v1/catalog/products/11000000-0000-0000-0000-000000000001` thành công trả về HTTP `200 OK` với đầy đủ thông tin chi tiết sản phẩm thật từ database Neon.

### 7. Ghi chú kỹ thuật về Hình ảnh Sản phẩm (Image Placeholder TODO)
- **Tình trạng hiện tại:** Chưa tích hợp dịch vụ lưu trữ đám mây (Cloud Storage / S3 / Cloudinary).
- **Vấn đề dữ liệu cũ:** Các sản phẩm seed sẵn trong database Neon đang mang link giả lập (`https://images.nexus.local/...`), domain này không có thực nên trình duyệt Frontend sẽ không hiển thị được ảnh (lỗi `ERR_NAME_NOT_RESOLVED`).
- **Giải pháp tạm thời:** Cấu hình fallback URL ảnh mặc định/placeholder (`https://placehold.co/...` hoặc ảnh mẫu online) trong `ProductService.cs`.
- **TODO sau này:** Khi triển khai tính năng upload ảnh (`CAT-12`), cấu hình Cloudinary / S3 và cập nhật lại toàn bộ link ảnh thật cho các sản phẩm trong database.

### 8. Hoàn thiện tính năng Lấy danh mục sản phẩm (CAT-19)
- **Tầng Application (`CategoryDtos.cs` & `CategoryService.cs`):**
  - Khai báo DTO `CategoryItemDto` hỗ trợ đệ quy thuộc tính `Children`.
  - Triển khai logic phân tách: Nếu `tree=true` dựng cây danh mục phân cấp cha-con; nếu `tree=false` trả danh sách phẳng (hỗ trợ lọc theo `parentId`).
- **Tầng API Controller (`CategoriesController.cs`):**
  - Chuyển `ListCategories` sang inject `ICategoryService` truy vấn dữ liệu từ DB Neon.
- **Kiểm thử thực tế:** Gọi endpoint `GET /api/v1/catalog/categories?tree=true` thành công trả về cấu trúc cây 200 OK.

### 9. Hoàn thiện tính năng Lấy thông tin biến thể hàng loạt cho Giỏ hàng & Đơn hàng (CAT-24)
- **Mục đích:** Hỗ trợ CartService và Order/FulfillmentService truy xuất nhanh thông tin chi tiết của danh sách SKU/Variant (Tên biến thể, Tên sản phẩm, Giá, Đơn vị tiền tệ, Thuộc tính, Ảnh đại diện, Phân loại bán...).
- **Tầng Application:**
  - Khai báo DTOs: `BatchGetVariantsRequest`, `BatchVariantItemDto`, `BatchVariantResponse`.
  - Khai báo `GetVariantsBatchAsync` trong `IProductService`.
  - Triển khai `GetVariantsBatchAsync` trong `ProductService.cs`: Truy vấn `_dbContext.ProductVariants` cùng `Product`, `Images`, `Attributes` với `.AsNoTracking()`. Tự động tách `NotFoundIds` cho các ID không tồn tại trong hệ thống. Tự động fallback ảnh đại diện sang placeholder nếu ảnh trỏ tới domain giả lập. Giới hạn tối đa 100 biến thể/request.
- **Tầng API Controller:**
  - Tạo mới `InternalVariantsController.cs` (`[Route("api/v1/internal/variants")]` & `[Route("api/v1/catalog/variants")]`).
  - Action `POST /api/v1/internal/variants` kiểm tra validation (bắt buộc có ít nhất 1 ID, không vượt quá 100 ID), trả về chuẩn phong bì `ApiResponse<BatchVariantResponse>`.
- **Kiểm thử thực tế:** Gọi POST với variant ID thật `11100000-0000-0000-0000-000000000001` và 1 ID giả lập: Trả về HTTP 200 OK, map đúng toàn bộ thông tin biến thể và đưa ID giả lập vào `notFoundIds`.

### 10. Hoàn thiện tính năng Kiểm tra trạng thái mua được của biến thể (CAT-39)
- **Mục đích:** Cho phép CartService, Checkout/OrderService, AuctionService kiểm tra tức thời xem biến thể có còn khả dụng để mua hay không trước khi thêm vào giỏ hoặc đặt hàng.
- **Tầng Application:**
  - Khai báo DTOs: `CheckVariantStatusRequest`, `VariantStatusItemDto`, `CheckVariantStatusResponse`.
  - Khai báo `CheckVariantStatusBatchAsync` trong `IProductService`.
  - Triển khai `CheckVariantStatusBatchAsync` trong `ProductService.cs`: Kiểm tra logic mua được (`IsPurchasable` và `Reason`):
    - `DELETED`: Variant hoặc Product đã bị xóa mềm (`IsDeleted == true`).
    - `PRODUCT_NOT_ACTIVE`: Sản phẩm cha chưa kích hoạt hoặc bị khóa.
    - `VARIANT_NOT_ACTIVE`: Biến thể chưa kích hoạt (`Status != Active` hoặc `IsActive == false`).
    - `NOT_FOR_REGULAR_SALE`: Mục đích là `CART`/`CHECKOUT` nhưng sản phẩm thuộc hình thức bán khác (ví dụ: `Auction`).
    - `PRICE_MISSING`: Mục đích là `CART`/`CHECKOUT` nhưng biến thể chưa có giá bán hợp lệ (> 0).
    - `NOT_FOR_AUCTION`: Mục đích là `AUCTION` nhưng sản phẩm không phải hình thức đấu giá.
    - `OK`: Thỏa mãn toàn bộ điều kiện mua.
- **Tầng API Controller:**
  - Action `POST /api/v1/internal/variants/check-status` trong `InternalVariantsController.cs`.
  - Validation: Bắt buộc mục đích mua thuộc `CART`, `CHECKOUT`, hoặc `AUCTION`.
- **Kiểm thử thực tế:**
  - Test với `purpose = "CART"`: Trả về `isPurchasable: true`, `reason: "OK"`.
  - Test với `purpose = "AUCTION"` cho sản phẩm thông thường: Trả về `isPurchasable: false`, `reason: "NOT_FOR_AUCTION"`.
  - Test validation lỗi: Trả về HTTP 400 Bad Request với mã lỗi chuẩn `VAL_INVALID_INPUT` và câu thông báo tiếng Anh chuẩn Sentence case.

### 11. Hoàn thiện luồng Tạo, Cập nhật, Xóa mềm sản phẩm và Kiểm tra phân quyền Seller
- **Mục đích:** Cho phép Seller tạo sản phẩm mới, cập nhật sản phẩm của chính mình và xóa mềm sản phẩm; ngăn chặn triệt để hành vi can thiệp trái phép vào sản phẩm của seller khác.
- **Tầng Application (`ProductDtos.cs` & `ProductService.cs`):**
  - Khai báo enum `ProductOperationStatus` (`Success`, `NotFound`, `Forbidden`, `InvalidCategory`) và `ProductOperationResult<T>`.
  - Khai báo commands: `CreateProductCommand`, `UpdateProductCommand`.
  - Cài đặt `CreateAsync`: Xác thực danh mục tồn tại trong `Categories`, gán `SellerId`, tạo mới entity `Product` với `Status = Draft`.
  - Cài đặt `UpdateAsync`: Kiểm tra sản phẩm tồn tại và kiểm tra quyền sở hữu (`product.SellerId == command.CurrentSellerId`). Nếu không trùng trả về `Forbidden`. Cập nhật các trường thông tin và thời gian `UpdatedAt`, `UpdatedBy`.
  - Cài đặt `SoftDeleteAsync`: Kiểm tra quyền sở hữu. Đánh dấu xóa mềm `IsDeleted = true`, `DeletedAt = DateTimeOffset.UtcNow` cho cả product và các variants con.
- **Tầng API Controller (`ProductsController.cs` & `HttpContextExtensions.cs`):**
  - Tạo helper `HttpContextExtensions.GetCurrentSellerId` trích xuất danh tính seller từ header Gateway `X-Seller-Id` / `X-User-Id`, JWT Claims, hoặc query param.
  - Loại bỏ hoàn toàn sự phụ thuộc vào `MockCatalogStore` khỏi `ProductsController`.
  - Thêm cấu hình `JsonStringEnumConverter` cho MVC Controllers trong `Program.cs`.
  - Action `POST /api/v1/catalog/products`: Yêu cầu danh tính seller, validate đầu vào, trả về HTTP 201 Created.
  - Action `PUT /api/v1/catalog/products/{id}`: Trả về 401 nếu thiếu header `X-Seller-Id`, trả về 403 `PERMISSION_DENIED` nếu seller khác cố sửa, trả về 200 OK khi cập nhật thành công.
  - Action `DELETE /api/v1/catalog/products/{id}`: Trả về 403 `PERMISSION_DENIED` nếu seller khác cố xóa, trả về 200 OK khi xóa mềm thành công.
  - Chuyển action `GET /api/v1/catalog/products/seller/{sellerId}` sang truy vấn trực tiếp qua `_productService.SearchAsync`.
- **Kiểm thử tự động thực tế (Database Neon):**
  - Tạo sản phẩm bởi Seller A: HTTP 201 Created (ID: `38d90630-29ce-4514-a53f-2d9c60749f66`, Status: `Draft`).
  - Gọi PUT không có header: Bị chặn đúng HTTP 401 `UNAUTHORIZED`.
  - Seller B cố tình PUT sản phẩm của Seller A: Bị chặn đúng HTTP 403 `PERMISSION_DENIED` ("You do not have permission to modify this product.").
  - Seller A gọi PUT: Cập nhật thành công HTTP 200 OK (Status chuyển sang `Active`).
  - Seller B cố tình DELETE sản phẩm của Seller A: Bị chặn đúng HTTP 403 `PERMISSION_DENIED`.
  - Seller A gọi DELETE: Xóa mềm thành công HTTP 200 OK.
  - Gọi lại GET sản phẩm vừa xóa: Trả về đúng HTTP 404 `PRODUCT_NOT_FOUND`.

### 12. Tích hợp Cloudinary & Hoàn thiện luồng Upload/Xóa Ảnh sản phẩm
- **Cấu hình Cloudinary:**
  - Cấu hình Cloud Name: `u6yxkkop`, API Key: `575673659418956`, API Secret: `F85vBI67YdxyHTKX4j4d5gG0QKM` vào `appsettings.json` và `appsettings.example.json`.
- **Cài đặt thư viện:**
  - Cài đặt package chính thức `CloudinaryDotNet` (v1.29.3) vào project `Catalog.Infrastructure`.
- **Tầng Application (`IImageStorageService.cs` & `ProductDtos.cs`):**
  - Khai báo interface `IImageStorageService` với các phương thức `UploadAsync` và `DeleteAsync`.
  - Khai báo DTO `CloudImageUploadResult` và `ProductImageDto`.
  - Thêm phương thức `AddProductImageAsync` và `DeleteProductImageAsync` vào `IProductService`.
- **Tầng Infrastructure (`CloudinaryStorageService.cs`):**
  - Cài đặt `CloudinaryStorageService` khởi tạo từ `Account(cloudName, apiKey, apiSecret)`.
  - Upload ảnh vào folder `nexus_catalog/products` với cấu hình tự động tối ưu hóa định dạng (`f_auto`) và chất lượng (`q_auto`).
  - Đăng ký DI `services.AddScoped<IImageStorageService, CloudinaryStorageService>()` trong `DependencyInjection.cs`.
- **Tầng Controller (`ProductsController.cs`):**
  - Endpoint `POST /api/v1/catalog/products/{id}/images`: Nhận file multipart (`IFormFile`), validate định dạng (`.jpg`, `.jpeg`, `.png`, `.webp`), dung lượng tối đa 5MB. Kiểm tra quyền sở hữu Seller trước khi upload và lưu URL an toàn vào bảng `ProductImages` trên database Neon.
  - Endpoint `DELETE /api/v1/catalog/products/{id}/images/{imageId}`: Kiểm tra quyền sở hữu Seller và xóa ảnh khỏi database.
- **Kiểm thử tự động thực tế (Cloudinary + Neon DB):**
  - Seller B cố tình upload ảnh cho sản phẩm của Seller A: Bị chặn đúng HTTP 403 `PERMISSION_DENIED`.
  - Seller A upload file ảnh thật: Thành công HTTP 201 Created, trả về URL CDN thật từ Cloudinary: `https://res.cloudinary.com/u6yxkkop/image/upload/v1793092110/nexus_catalog/products/jc7g5k0qa17tmdst4m1b.png`.
  - Gọi `GET /api/v1/catalog/products/{id}`: Xác nhận ảnh hiển thị chính xác trong mảng `images` của sản phẩm.
  - Seller A gọi DELETE xóa ảnh: Thành công HTTP 200 OK.
  - Gọi lại GET sản phẩm: Xác nhận ảnh đã bị loại bỏ khỏi danh sách ảnh khả dụng.

### 13. Hoàn thiện toàn bộ chu trình quản lý Biến thể (CAT-07, CAT-08, CAT-09, CAT-10, CAT-11)
- **Mục đích:** Xây dựng đầy đủ các chức năng quản trị biến thể (SKU) cho sản phẩm bán thường (REGULAR) theo đặc tả chuẩn SRS Excel, kết nối trực tiếp database Neon PostgreSQL.
- **Tầng Application (`ProductDtos.cs` & `ProductService.cs`):**
  - Khai báo enum `VariantOperationStatus` (`Success`, `NotFound`, `Forbidden`, `DuplicateSku`, `InvalidOperation`, `HasOrder`, `LastVariant`) và `VariantOperationResult<T>`.
  - Khai báo các commands/DTOs: `CreateVariantRequest`, `UpdateVariantRequest`, `ChangeVariantStatusRequest`, `SetDefaultVariantRequest`, `VariantDetailDto`.
  - **`CAT-07` (`AddVariantAsync`)**: Thêm biến thể mới, kiểm tra `SaleType == Regular`, kiểm tra `SkuCode` duy nhất, `Price > 0`, lưu `ProductVariants` và thuộc tính động `ProductVariantAttributes`. Tự động gán mặc định nếu là variant đầu tiên.
  - **`CAT-08` (`UpdateVariantAsync`)**: Kiểm tra Seller sở hữu sản phẩm cha, cập nhật tên, giá bán, tiền tệ và ghi đè danh sách thuộc tính `Attributes`.
  - **`CAT-09` (`ChangeVariantStatusAsync`)**: Bật `ACTIVE` hoặc ngưng bán `INACTIVE` kèm lý do `StatusReason` (`OUT_OF_STOCK`, `DISCONTINUED`, `OTHER`). Chặn không cho tắt variant cuối cùng còn bán khi sản phẩm cha đang `ACTIVE`.
  - **`CAT-10` (`SetDefaultVariantAsync`)**: Cập nhật cờ `IsActive = true` cho variant được chỉ định và gán `false` cho các variant còn lại.
  - **`CAT-11` (`SoftDeleteVariantAsync`)**: Kiểm tra biến thể chưa từng có đơn hàng (`HasOrder == false`), chặn nếu là biến thể duy nhất (`LAST_VARIANT`). Đánh dấu `IsDeleted = true` và tự động chuyển cờ mặc định sang variant còn lại nếu xóa trúng variant mặc định.
- **Tầng API Controller (`ProductsController.cs` & `VariantsController.cs`):**
  - `POST /api/v1/catalog/products/{id}/variants`: Thêm variant (CAT-07).
  - `PUT /api/v1/catalog/products/{id}/default-variant`: Đặt variant mặc định (CAT-10).
  - `PUT /api/v1/catalog/variants/{id}`: Cập nhật variant (CAT-08).
  - `PATCH /api/v1/catalog/variants/{id}/status`: Đổi trạng thái variant (CAT-09).
  - `DELETE /api/v1/catalog/variants/{id}`: Xóa mềm variant (CAT-11).
- **Kiểm thử tự động thực tế (Database Neon):**
  - `CAT-07`: Tạo Variant 1 thành công (HTTP 201, `isDefault: true`).
  - `CAT-07`: Tạo Variant 2 thành công (HTTP 201, `isDefault: false`).
  - `CAT-07`: Thử tạo trùng SkuCode $\rightarrow$ Bị chặn đúng HTTP 409 `SKU_CODE_DUPLICATED`.
  - `CAT-08`: Seller B sửa trái phép $\rightarrow$ Bị chặn đúng HTTP 403 `PERMISSION_DENIED`.
  - `CAT-08`: Seller A sửa giá và thuộc tính $\rightarrow$ Thành công HTTP 200 OK.
  - `CAT-10`: Đặt Variant 2 làm mặc định $\rightarrow$ Thành công HTTP 200 OK, xác nhận `IsDefault` đổi chính xác.
  - `CAT-09`: Đổi Variant 1 sang `INACTIVE` (`OUT_OF_STOCK`) $\rightarrow$ Thành công HTTP 200 OK.
  - `CAT-09`: Thử tắt nốt Variant 2 khi sản phẩm đang Active $\rightarrow$ Bị chặn đúng HTTP 400 `VAL_INVALID_INPUT`.
  - `CAT-11`: Xóa mềm Variant 1 $\rightarrow$ Thành công HTTP 200 OK.
  - `CAT-11`: Thử xóa nốt Variant 2 (variant duy nhất còn lại) $\rightarrow$ Bị chặn đúng HTTP 409 `LAST_VARIANT`.

---

### 14. Khắc phục lỗi Swagger Generation (HTTP 500 khi load swagger.json)
- **Nguyên nhân**: Swashbuckle 6.6.2 trên .NET 8 ném ngoại lệ `SwaggerGeneratorException` khi phát hiện parameter `[FromForm] IFormFile? file` được đặt trực tiếp trên phương thức Controller `UploadProductImage` mà không dùng class model bọc lại.
- **Giải pháp**:
  - Tạo class `UploadProductImageRequest` chứa `IFormFile? File`, `Guid? VariantId`, `string? AltText`, `short SortOrder = 0`.
  - Cập nhật signature `UploadProductImage([FromRoute] Guid id, [FromForm] UploadProductImageRequest request, ...)` trong [`ProductsController.cs`](file:///d:/Apartment/FALL_26/FSA/Project/CatalogService/Catalog.Api/Controllers/ProductsController.cs).
- **Kiểm chứng**:
  - `dotnet build Catalog.sln -c Debug`: 0 Warning, 0 Error.
  - Test trực tiếp `GET http://localhost:5272/swagger/v1/swagger.json`: Trả về HTTP 200 OK và Swagger schema đầy đủ.

---

## 📅 Ngày: 28/10/2026 (Chiều & Tối)

### 15. Hoàn thiện API Quản lý Sản phẩm dành riêng cho Seller (CAT-44 & CAT-45)
- **Mục đích:** Cho phép Seller xem danh sách sản phẩm thuộc quyền sở hữu của chính mình (hỗ trợ phân trang, lọc theo trạng thái `Status`, lọc theo hình thức bán `SaleType`, tìm kiếm theo từ khóa) và xem chi tiết sản phẩm thuộc shop mình (kể cả sản phẩm đang ở trạng thái `Draft` hoặc `Inactive`).
- **Tầng Application (`ProductDtos.cs` & `ProductService.cs`):**
  - Khai báo DTOs: `SellerProductListQuery`, `SellerProductListItemDto`.
  - Khai báo và cài đặt `GetSellerProductsAsync`: Truy vấn trực tiếp `_dbContext.Products` lọc theo `SellerId == currentSellerId`, không bao gồm sản phẩm đã xóa mềm (`IsDeleted == false`), eager loading `Category`, `Variants`, `Images` để tính toán khoảng giá min-max, tổng số lượng biến thể và lấy ảnh đại diện.
  - Khai báo và cài đặt `GetSellerProductDetailAsync`: Lấy chi tiết sản phẩm, xác thực quyền sở hữu `product.SellerId == currentSellerId`, trả về đầy đủ variants, attributes, images.
- **Tầng API Controller (`SellerProductsController.cs`):**
  - Tạo mới `SellerProductsController.cs` (`[Route("api/v1/seller/products")]`).
  - Endpoint `GET /api/v1/seller/products`: Lấy danh sách sản phẩm của Seller từ `X-Seller-Id` header (hoặc query param), trả về phân trang chuẩn `ApiResponse<PagedData<SellerProductListItemDto>>`.
  - Endpoint `GET /api/v1/seller/products/{id}`: Xem chi tiết sản phẩm của Seller, chặn HTTP 403 nếu cố truy cập sản phẩm của seller khác.
- **Kiểm chứng thực tế:**
  - Gọi `GET /api/v1/seller/products` với seller `cccccccc-3333-3333-3333-333333333333`: Trả về 200 OK với danh sách sản phẩm thật trên DB Neon.

### 16. Hoàn thiện nghiệp vụ Chuyển đổi Trạng thái Sản phẩm (CAT-03)
- **Mục đích:** Hỗ trợ Seller chuyển đổi trạng thái sản phẩm giữa `Draft`, `Active`, `Inactive` với các quy tắc ràng buộc chặt chẽ nhằm đảm bảo tính toàn vẹn dữ liệu trước khi mở bán.
- **Tầng Application (`ProductDtos.cs` & `ProductService.cs`):**
  - Khai báo command: `ChangeProductStatusRequest(ProductStatus TargetStatus, string? Reason)`.
  - Cài đặt phương thức `ChangeProductStatusAsync`:
    - Xác thực quyền sở hữu của Seller (`product.SellerId == currentSellerId`).
    - Quy tắc chuyển đổi sang `Active`:
      - Sản phẩm bắt buộc phải có ít nhất 1 biến thể (Variant) đang ở trạng thái `Active`.
      - Các biến thể active phải có giá bán hợp lệ (`Price > 0`).
      - Bắt buộc phải có ít nhất 1 hình ảnh (`ProductImages`).
      - Nếu vi phạm bất kỳ điều kiện nào $\rightarrow$ Trả về `InvalidOperation` kèm thông báo lỗi cụ thể (ví dụ: `Cannot activate product without any active variants.` hoặc `Cannot activate product without images.`).
    - Quy tắc chuyển đổi sang `Inactive`:
      - Yêu cầu lý do thay đổi `Reason`.
      - Cập nhật trạng thái `Status = Inactive`.
- **Tầng API Controller (`ProductsController.cs`):**
  - Endpoint `PATCH /api/v1/products/{id}/status`: Nhận request body, kiểm tra quyền Seller, trả về `ApiResponse<ProductDetailDto>`.
- **Kiểm chứng thực tế:**
  - Test kích hoạt sản phẩm chưa có biến thể: Bị chặn đúng HTTP 400 `VAL_INVALID_INPUT`.
  - Test kích hoạt sản phẩm thỏa mãn điều kiện: Thành công HTTP 200 OK, `status` chuyển thành `Active`.

### 17. Hoàn thiện Cập nhật Thông tin Sản phẩm & Kiểm tra Danh mục lá (CAT-02)
- **Tầng Application (`ProductService.cs`):**
  - Tích hợp kiểm tra danh mục khi Seller đổi `CategoryId`:
    - Danh mục phải tồn tại trong bảng `Categories` và chưa bị xóa mềm.
    - Danh mục phải là danh mục lá (Leaf Category) — tức không được có danh mục con (`!_dbContext.Categories.Any(c => c.ParentId == categoryId)`).
    - Nếu danh mục có chứa danh mục con $\rightarrow$ Chặn với mã lỗi `VAL_INVALID_INPUT` ("Cannot assign product to a parent category. Please select a leaf category.").
- **Kiểm chứng thực tế:** Đổi danh mục thành danh mục cha cấp 1 $\rightarrow$ Bị chặn đúng chuẩn validation.

### 18. Tối ưu Quy chuẩn Duy nhất cho Category Name & SKU Code
- **Category Name Uniqueness:** Kiểm tra không trùng tên danh mục (không phân biệt hoa thường, `StringComparison.OrdinalIgnoreCase`) trên toàn hệ thống trong `CategoriesController`.
- **SKU Code Per-Seller:** Đảm bảo `SkuCode` là duy nhất theo từng Seller (`_dbContext.ProductVariants.Any(v => v.SkuCode == sku && v.Product.SellerId == sellerId)`), hỗ trợ người bán khác nhau có thể trùng mã quy ước nội bộ mà không bị xung đột.

---

## 📅 Ngày: 29/10/2026 (Sáng & Trưa)

### 19. Chuẩn hóa & Dọn dẹp Route Swagger (Xóa bỏ Route Alias trùng lặp)
- **Vấn đề:** Các controller trước đó khai báo đồng thời 2 Route attribute: `[Route("api/v1/catalog/...")]` và `[Route("api/v1/...")]`. Việc này khiến Swagger UI sinh ra gấp đôi số lượng endpoint (mỗi API xuất hiện 2 lần), gây rối mắt và khó phân biệt cho Frontend/Tester.
- **Xử lý:**
  - Đồng bộ loại bỏ toàn bộ tiền tố alias `/api/v1/catalog/...` trên tất cả 6 Controllers:
    - [`CategoriesController.cs`](file:///d:/Apartment/FALL_26/FSA/Project/CatalogService/Catalog.Api/Controllers/CategoriesController.cs): `[Route("api/v1/categories")]`
    - [`ProductsController.cs`](file:///d:/Apartment/FALL_26/FSA/Project/CatalogService/Catalog.Api/Controllers/ProductsController.cs): `[Route("api/v1/products")]`
    - [`SellerProductsController.cs`](file:///d:/Apartment/FALL_26/FSA/Project/CatalogService/Catalog.Api/Controllers/SellerProductsController.cs): `[Route("api/v1/seller/products")]`
    - [`VariantsController.cs`](file:///d:/Apartment/FALL_26/FSA/Project/CatalogService/Catalog.Api/Controllers/VariantsController.cs): `[Route("api/v1/variants")]`
    - [`InternalVariantsController.cs`](file:///d:/Apartment/FALL_26/FSA/Project/CatalogService/Catalog.Api/Controllers/InternalVariantsController.cs): `[Route("api/v1/internal/variants")]`
    - [`InventoryController.cs`](file:///d:/Apartment/FALL_26/FSA/Project/CatalogService/Catalog.Api/Controllers/InventoryController.cs): `[Route("api/v1/inventory")]`
- **Kết quả:** Swagger UI giảm 50% số endpoint trùng lặp, giao diện hiển thị gọn gàng, đúng chuẩn RESTful API.

### 20. Chuyển đổi toàn diện `CategoriesController` sang 100% Database Thật (Neon PostgreSQL)
- **Hiện trạng cũ:** `CategoriesController` vẫn phụ thuộc `MockCatalogStore` (singleton nạp file JSON tĩnh). Khi gọi PUT/DELETE với ID thật trong database Neon (`10000000-0000-0000-0000-000000000001`), hàm trả về 404 `CATEGORY_NOT_FOUND` vì mock store chỉ chứa các ID giả lập (`aaaaaaaa...`).
- **Giải pháp:**
  - Loại bỏ hoàn toàn `MockCatalogStore` khỏi `CategoriesController`.
  - Inject trực tiếp `ICatalogDbContext` và `IPublishEndpoint` (MassTransit).
  - **`CreateCategory` (POST):** Kiểm tra tên danh mục duy nhất trong DB thật, thêm mới `Category` vào `_dbContext.Categories`, gọi `_dbContext.SaveChangesAsync()`, và phát sự kiện `CategoryCreatedEvent` lên RabbitMQ message bus.
  - **`UpdateCategory` (PUT):** Truy vấn `_dbContext.Categories`, kiểm tra tồn tại (nếu không có trả về 404), kiểm tra trùng tên danh mục, cập nhật dữ liệu và thời gian `UpdatedAt`.
  - **`DeleteCategory` (DELETE):** Truy vấn `_dbContext.Categories`, đánh dấu xóa mềm `IsDeleted = true`, `DeletedAt = DateTimeOffset.UtcNow`, lưu thay đổi.
- **Kiểm chứng thực tế:** Cập nhật danh mục với ID thật `10000000-0000-0000-0000-000000000001` thành công HTTP 200 OK trên database Neon.

### 21. Khởi tạo Tài liệu Đặc tả Chức năng Toàn diện (CATALOG_FUNCTIONS & CAT_FUNCTIONS_DETAILS)
- **Xây dựng [`CATALOG_FUNCTIONS.md`](file:///d:/Apartment/FALL_26/FSA/Project/CatalogService/CATALOG_FUNCTIONS.md):**
  - Tổng hợp 25 chức năng CAT đối chiếu giữa SRS Excel và mã nguồn thực tế.
  - Bảng ma trận đối chiếu chi tiết: Mã CAT, Tên chức năng, Phương thức HTTP, Route chuẩn, Trạng thái (100% Real DB / Mock / Event), Controller & Action tương ứng.
- **Xây dựng [`CAT_FUNCTIONS_DETAILS.txt`](file:///d:/Apartment/FALL_26/FSA/Project/CatalogService/CAT_FUNCTIONS_DETAILS.txt):**
  - Tài liệu kỹ thuật chi tiết độc lập (34KB) gồm 4 phần:
    - **Phần A - Cloudinary:** Cấu hình, interface `IImageStorageService`, class `CloudinaryStorageService`, luồng upload multipart, validation quy định, cơ chế CDN HTTPS.
    - **Phần B - RabbitMQ / MassTransit:** Đăng ký In-Memory fallback cho local, EF Core Outbox cho production, `CategoryCreatedEvent`, `CategoryCreatedConsumer`, kebab-case convention.
    - **Phần C - 25 CAT Functions:** Liệt kê đầy đủ file controller, service, tên hàm và trích xuất nguyên văn các dòng code validation đặc biệt quan trọng (kiểm tra quyền seller, kiểm tra variant active, kiểm tra giá > 0, kiểm tra leaf category...).
    - **Phần D - Error Codes:** Bảng mã lỗi chuẩn `SCREAMING_SNAKE_CASE` toàn hệ thống.

### 22. Đồng bộ Git & Clean Workspace
- Xóa bỏ thư mục clone lồng vô tình (`CatalogService/CatalogService/`).
- Tạo các commit rõ ràng, ngữ nghĩa:
  - Commit `4460736`: `feat(catalog): use real database for categories and remove redundant route aliases`
  - Commit `b58a019`: `docs: add comprehensive CAT functions implementation details, Cloudinary setup, and RabbitMQ event guide`
- Đẩy nhánh thành công lên remote `origin/dev-quynh`. Trạng thái workspace sạch sẽ (`git status` clean).






