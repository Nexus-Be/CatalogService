# 00 — NHIỆM VỤ CỐT LÕI & RÀO CHẮN CHỐNG ẢO GIÁC (CATALOG SERVICE)

---

## 1. NHIỆM VỤ & RANH GIỚI MIỀN (BOUNDED CONTEXT)

**CatalogService** chịu trách nhiệm độc quyền về việc quản lý danh mục sản phẩm, thông tin sản phẩm và kho hàng ảo của người bán (Seller Inventory):
- Quản lý cây danh mục sản phẩm (`Category` có phân cấp cha - con `ParentId`).
- Quản lý thông tin chi tiết sản phẩm (`Product`): Tên, mô tả, giá bán, tiền tệ, slug URL, seller sở hữu.
- Quản lý kho hàng ảo (`Inventory`): Số lượng hàng có sẵn để bán (`Available`), cập nhật số lượng khi seller nhập/xuất kho.
- Tham gia vào quy trình **Checkout Saga**: Lắng nghe sự kiện tạo đơn từ `CommerceService` để tạm giữ kho (`ReserveStock`), và nhả lại kho (`ReleaseStock`) nếu thanh toán thất bại.
- Cung cấp dịch vụ gRPC Server tại cổng `8002` cho ApiGateway.

---

## 2. NHỮNG ĐIỀU CẤM KỴ ĐỐI VỚI AI AGENTS (ANTI-HALLUCINATION GUARDRAILS)

1. **TUYỆT ĐỐI KHÔNG DÙNG REPOSITORY PATTERN BỌC NGOÀI EF CORE**:
   - Tuân thủ quyết định kiến trúc [ADR.md](../../ApiGateway/ADR.md).
   - Sử dụng trực tiếp `CatalogDbContext` và `DbSet<T>` trong tầng Application.
   - Không được tạo `IProductRepository`, `ICategoryRepository`, `UnitOfWork`.

2. **KHÔNG NHẦM LẪN GIỮA KHO ẢO (CATALOG) VÀ KHO VẬT LÝ (FULFILLMENT)**:
   - `CatalogService` chỉ quản lý số lượng tồn kho ảo có sẵn để bán trên sàn thương mại điện tử (`Available`).
   - Kho bãi vật lý thực tế (kích thước, trọng lượng kiện hàng, vị trí giá kệ, mã tracking) thuộc về `FulfillmentService`. Tuyệt đối không đưa logic tính phí ship hay theo dõi vận đơn vào CatalogService.

3. **TUYỆT ĐỐI KHÔNG THAY ĐỔI CỔNG 8002 VÀ DATABASE 5433**:
   - Service gRPC chạy tại `http://localhost:8002`.
   - Chuỗi kết nối DB trỏ tới `localhost:5433/category_db`.

4. **TUYỆT ĐỐI KHÔNG TỰ Ý SỬA PROTOBUF MÀ KHÔNG ĐỒNG BỘ GATEWAY**:
   - RPC methods và message formats phải khớp 100% với `Contracts/shop.proto` tại ApiGateway.
