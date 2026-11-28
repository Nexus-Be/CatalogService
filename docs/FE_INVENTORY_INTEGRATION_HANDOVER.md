# THÔNG BÁO BÀN GIAO TÍCH HỢP TỒN KHO TRÊN CÁC ENDPOINT SẢN PHẨM (CATALOG SERVICE -> FRONTEND)

> **Dự án:** Sàn thương mại điện tử & Đấu giá Nexus  
> **Dịch vụ bàn giao:** CatalogService (Port `8002`, Database `category_db:5434`)  
> **Người gửi:** Catalog Service Backend Engineering Team  
> **Người nhận:** Frontend Engineering Team (Web / Mobile)  
> **Ngày cập nhật:** 28/11/2026  
> **Trạng thái:** ✅ Đã hoàn tất triển khai & tích hợp liên thông kho thành công trên nhánh `dev`  

---

## 1. Bối cảnh & Hiện trạng

Trước đây, CatalogService chưa tích hợp truy vấn số lượng tồn kho theo thời gian thực từ FulfillmentService trên các endpoint đọc sản phẩm. Do đó:
- Phía Frontend (Buyer) phải hiển thị tạm một dòng chữ hardcode:
  > `"Chưa xác định tồn kho tổng hợp. Hệ thống sẽ kiểm tra khi thêm vào giỏ."` (tại `src/features/catalog/ProductDetailScreen.tsx`).
- Người mua không biết được sản phẩm/biến thể còn bao nhiêu chiếc hoặc đã hết hàng hay chưa cho đến khi bấm "Thêm vào giỏ" và nhận lỗi từ CommerceService.

**Hiện tại (Bản cập nhật mới nhất trên nhánh `dev`):**
CatalogService đã hoàn tất cơ chế **Enrich tồn kho tự động** trực tiếp từ FulfillmentService cho toàn bộ các endpoint đọc sản phẩm (Public Buyer, Seller, Admin) với cơ chế Batch Query O(1) và chịu lỗi mềm (Fault-tolerant Fallback).

Tài liệu này hướng dẫn chi tiết hợp đồng dữ liệu mới và các bước phía Frontend cần cập nhật để hiển thị tồn kho chính xác và khóa mua khi hết hàng.

---

## 2. Hợp đồng Dữ liệu Mới (Enriched DTO Contract)

### 2.1. Phân biệt ý nghĩa các trường số lượng

| Trường dữ liệu | Kiểu | Mô tả nghiệp vụ | Phạm vi hiển thị |
| :--- | :---: | :--- | :--- |
| `totalQuantity` | `number` | Tổng số lượng sản phẩm vật lý thực tế trong kho của người bán. | Buyer, Seller, Admin |
| `availableQuantity` | `number` | Số lượng hàng có sẵn để bán ngay (`totalQuantity - reservedQuantity`). Dùng để kiểm tra còn hàng / hết hàng và chặn tăng số lượng. | **Buyer**, Seller, Admin |
| `reservedQuantity` | `number` | Số lượng hàng đang được giữ cho các đơn hàng chờ thanh toán / chờ xuất kho. | **Chỉ dành cho Seller & Admin** |

---

### 2.2. Danh sách các Endpoints đã được bổ sung dữ liệu tồn kho

#### A. Phân hệ Người mua (Buyer Public APIs)
1. **Danh sách sản phẩm / Tìm kiếm:**
   - `GET /api/v1/products`
   - `GET /api/v1/products/search`
   - **Dữ liệu trả về trong từng item:**
     ```json
     {
       "productId": "159a483a-5776-4516-a62a-c8a490476694",
       "name": "Gối ôm công thái học",
       "minPrice": 200000.00,
       "maxPrice": 200001.00,
       "totalQuantity": 50,
       "availableQuantity": 50
     }
     ```
2. **Chi tiết sản phẩm:**
   - `GET /api/v1/products/{id}`
   - **Dữ liệu trả về:** Có tồn kho ở cả cấp sản phẩm tổng hợp và cấp từng biến thể SKU:
     ```json
     {
       "productId": "159a483a-5776-4516-a62a-c8a490476694",
       "name": "Gối ôm công thái học",
       "totalQuantity": 50,
       "availableQuantity": 50,
       "variants": [
         {
           "variantId": "7573f375-1d93-4662-b442-c03fe6153d4c",
           "skuCode": "GOI-OM-CONG-TIEU-CHUAN-FKSJ",
           "name": "Màu xám",
           "price": 200000.00,
           "totalQuantity": 50,
           "availableQuantity": 50
         },
         {
           "variantId": "9a0a9747-43ef-44a8-be7f-13c93a6c36ac",
           "skuCode": "GOI-OM-CONG-PHIEN-BAN-QF4G",
           "name": "Màu xanh",
           "price": 200001.00,
           "totalQuantity": 0,
           "availableQuantity": 0
         }
       ]
     }
     ```
3. **Chi tiết biến thể đơn lẻ:**
   - `GET /api/v1/variants/{id}`
   - Trả về `totalQuantity` và `availableQuantity` của biến thể.

#### B. Phân hệ Người bán (Seller APIs)
- `GET /api/v1/seller/products`: Trả về `totalQuantity`, `availableQuantity`, `reservedQuantity`.
- `GET /api/v1/seller/products/{id}`: Trả về `totalQuantity`, `availableQuantity`, `reservedQuantity` cho cả cấp sản phẩm và từng variant trong mảng `variants`.

#### C. Phân hệ Quản trị viên (Admin APIs)
- `GET /api/v1/admin/products`: Trả về `totalQuantity`, `availableQuantity`, `reservedQuantity`.

---

## 3. Danh mục Công việc Cần Cập nhật ở Frontend (Action Items for FE)

### 📌 Nhiệm vụ 1: Cập nhật Trang Chi tiết Sản phẩm (`src/features/catalog/ProductDetailScreen.tsx`)

#### 1. Bổ sung TypeScript Interface
Cập nhật `ProductVariant` và `ProductDetail` để nhận các trường số lượng:
```typescript
type ProductVariant = {
  variantId: string;
  skuCode: string;
  name: string;
  price: number | null;
  status: string;
  isActive: boolean;
  attributes?: Array<{ attrName: string; attrValue: string }>;
  primaryImageUrl?: string | null;
  images?: ProductImage[];
  totalQuantity?: number;
  availableQuantity?: number; // <-- MỚI
};

type ProductDetail = {
  productId: string;
  sellerId?: string | null;
  name: string;
  description?: string | null;
  brand?: string | null;
  saleType?: string;
  status?: string;
  specifications?: ProductSpecification[] | null;
  category?: { categoryId: string; name: string } | null;
  variants: ProductVariant[];
  images: ProductImage[];
  rating?: { avgRating: number | null; reviewCount: number } | null;
  totalQuantity?: number;
  availableQuantity?: number; // <-- MỚI
};
```

#### 2. Xóa bỏ dòng thông báo tạm hardcode
Tại vùng render thông tin đặt hàng, **xóa bỏ hoàn toàn** đoạn code cũ:
```tsx
// ❌ ĐOẠN CŨ CẦN XÓA HOẶC THAY THẾ:
<p className="product-detail-availability" role="status">
  {canAdd ? "Chưa xác định tồn kho tổng hợp. Hệ thống sẽ kiểm tra khi thêm vào giỏ." : ...}
</p>
```

#### 3. Cập nhật Logic Kiểm tra & Hiển thị Tồn kho (Tham khảo đề xuất)
```typescript
const selectedVariantStock = typeof selectedVariant?.availableQuantity === "number"
  ? selectedVariant.availableQuantity
  : undefined;

const isSelectedOutOfStock = selectedVariantStock !== undefined && selectedVariantStock <= 0;
const isProductOutOfStock = typeof product.availableQuantity === "number" && product.availableQuantity <= 0;
const hasExceededStock = selectedVariantStock !== undefined && parsedQuantity > selectedVariantStock;

// Điều kiện cho phép bấm "Thêm vào giỏ":
const canAdd = regularProduct &&
  Boolean(selectedVariant && purchasableVariant(selectedVariant)) &&
  validQuantity &&
  !isSelectedOutOfStock &&
  !hasExceededStock;

// Xây dựng câu thông báo trạng thái:
let availabilityMessage = "";
if (!regularProduct || availableVariants.length === 0) {
  availabilityMessage = "Sản phẩm hiện chưa thể mua.";
} else if (selectedVariant) {
  if (!purchasableVariant(selectedVariant)) {
    availabilityMessage = "Phiên bản này chưa thể mua.";
  } else if (isSelectedOutOfStock) {
    availabilityMessage = "Phiên bản này hiện đã hết hàng.";
  } else if (hasExceededStock) {
    availabilityMessage = `Số lượng chọn vượt quá số lượng trong kho (còn ${selectedVariantStock} sản phẩm).`;
  } else if (selectedVariantStock !== undefined) {
    availabilityMessage = `Kho khả dụng: còn ${selectedVariantStock} sản phẩm.`;
  }
} else if (isProductOutOfStock) {
  availabilityMessage = "Sản phẩm hiện đã hết hàng.";
} else if (canChooseAttributes) {
  availabilityMessage = "Chọn màu sắc và kích cỡ để thêm vào giỏ.";
} else {
  availabilityMessage = "Chọn phiên bản để thêm vào giỏ.";
}
```

#### 4. Khóa/giới hạn cụm nút chọn số lượng và nút đặt hàng
- **Nút tăng số lượng (`+`)**: Disable khi `parsedQuantity >= selectedVariantStock` hoặc khi `isSelectedOutOfStock`.
- **Nhãn hiển thị tồn kho cạnh nút chọn số lượng**:
  ```tsx
  {selectedVariant && selectedVariantStock !== undefined && (
    <span style={{ fontSize: "13px", color: isSelectedOutOfStock ? "#b91c1c" : "var(--muted)" }}>
      {isSelectedOutOfStock ? "Hết hàng" : `(Còn ${selectedVariantStock} sản phẩm có sẵn)`}
    </span>
  )}
  ```
- **Nút đặt mua / Thêm vào giỏ**:
  ```tsx
  <button
    type="button"
    className="button button-primary"
    disabled={!canAdd || adding}
    onClick={() => void addToCart()}
  >
    {adding ? "Đang thêm…" : isSelectedOutOfStock ? "Hết hàng" : "Thêm vào giỏ"}
  </button>
  ```
- **Dropdown chọn biến thể**: Hiển thị kèm trạng thái số lượng:
  ```tsx
  {product.variants.map((variant) => {
    const isVariantOut = typeof variant.availableQuantity === "number" && variant.availableQuantity <= 0;
    return (
      <option key={variant.variantId} value={variant.variantId} disabled={!regularProduct || !purchasableVariant(variant)}>
        {variant.name || variant.skuCode} ({variant.skuCode}) · {purchasableVariant(variant) ? `${formatMoney(variant.price)}${isVariantOut ? " (Hết hàng)" : typeof variant.availableQuantity === "number" ? ` (Còn ${variant.availableQuantity})` : ""}` : "Chưa thể mua"}
      </option>
    );
  })}
  ```

---

### 📌 Nhiệm vụ 2: Cập nhật Thẻ Sản phẩm ở Danh mục (`src/features/catalog/CatalogScreens.tsx`)

1. Cập nhật type `Product` thêm:
   ```typescript
   totalQuantity?: number;
   availableQuantity?: number;
   ```
2. Cập nhật nhãn trạng thái sản phẩm trên Card:
   ```typescript
   function productStatusLabel(product: Product): string {
     if (typeof product.availableQuantity === "number" && product.availableQuantity <= 0) {
       return "Hết hàng";
     }
     switch ((product.status ?? "").toUpperCase()) {
       case "ACTIVE": return "Đang bán";
       case "OUTOFSTOCK": case "OUT_OF_STOCK": return "Hết hàng";
       case "": return "";
       default: return product.status ?? "";
     }
   }
   ```

---

### 📌 Nhiệm vụ 3: Tối ưu hóa Kênh Người Bán (`src/features/seller/SellerProductDetailScreen.tsx`)

Hiện tại, response của `GET /api/v1/seller/products/{id}` đã chứa sẵn:
- Cấp sản phẩm: `totalQuantity`, `availableQuantity`, `reservedQuantity`.
- Cấp biến thể: mỗi phần tử trong `variants` đều đã có `totalQuantity`, `availableQuantity`, `reservedQuantity`.

FE team có thể cân nhắc tái sử dụng trực tiếp dữ liệu này thay vì phải gọi thêm API `sellerCatalogService.inventoryBatch(...)` rời rạc ở bên ngoài để giảm bớt tải mạng.

---

## 4. Hỗ trợ & Kiểm thử (Verification)

Backend CatalogService hiện đã hoạt động đồng bộ với FulfillmentService trên local Docker network (`nexus-network`):
- Để kiểm tra với sản phẩm có tồn kho: Xem sản phẩm **"Gối ôm công thái học"** (`159a483a-5776-4516-a62a-c8a490476694`) - biến thể màu xám (SKU `GOI-OM-CONG-TIEU-CHUAN-FKSJ`) hiện có tồn kho là **50**.
- Biến thể màu xanh của cùng sản phẩm có tồn kho là **0** (Hết hàng) để kiểm tra giao diện xử lý out-of-stock.
- Sản phẩm **"Áo Mec F1"** (`86330cef-87f0-4fdf-98f0-05c5572fbc6f`) hiện có tồn kho là **8**.

---

## 5. Bổ sung: Cơ chế Tự sinh mã SKU (SKU Auto-generation) & Endpoint Gợi ý SKU Mới

### 5.1. Lý do & Hiện trạng tester ghi nhận
- Trước đây, trang tạo sản phẩm ở Frontend tự sinh mã SKU bằng JavaScript (`buildAutoSku`) sử dụng `Math.random()`. Do FE không có cơ sở dữ liệu, việc tự sinh ở client có nguy cơ bị **trùng mã SKU (duplicate)** với các sản phẩm đã có của Seller, dẫn đến lỗi `DUPLICATE_SKU (409 Conflict)`.
- Backend CatalogService trước đây cũng bắt buộc `skuCode` không được rỗng, khiến FE bắt buộc phải gửi mã lên.

### 5.2. Thay đổi đã triển khai tại Backend CatalogService (Đã Live trên nhánh `dev`)

#### A. Cho phép để trống `skuCode` khi Tạo sản phẩm & Thêm biến thể
- Tại `POST /api/v1/products` và `POST /api/v1/products/{id}/variants`:
  - Trường `skuCode` nay là **TÙY CHỌN (Optional)**.
  - **Nếu Seller tự nhập SKU tùy chỉnh**: Backend vẫn tôn trọng và kiểm tra tính duy nhất trong shop như bình thường (`EnsureUniqueSkuInShopAsync`).
  - **Nếu Seller để trống SKU (`null`, `""`, hoặc khoảng trắng)**: Backend sẽ **tự động sinh mã SKU chuẩn** (dạng `[TÊN_SP]-[BIẾN_THỂ]-[HẬU_TỐ]`, ví dụ: `GOI-OM-XAM-BF78`). Backend truy vấn trực tiếp vào DB để **đảm bảo 100% không bao giờ bị trùng trong shop**.

#### B. Endpoint mới: Gợi ý mã SKU thời gian thực (Real-time Preview API)
- **Phương thức & Route:** `GET /api/v1/seller/products/suggest-sku`
- **Quyền hạn (RBAC):** `seller` hoặc `admin` (yêu cầu header xác thực user).
- **Query Parameters:**
  - `name` (optional): Tên sản phẩm (ví dụ: `Gối ôm công thái học`).
  - `variantName` (optional): Tên biến thể (ví dụ: `Màu xám`).
- **Response (200 OK):**
  ```json
  {
    "skuCode": "GOI-OM-CONG-MAU-XAM-BF78"
  }
  ```
  *(Mã SKU trả về đã được Backend kiểm tra đối chiếu trực tiếp với database và bảo đảm không trùng lặp với bất kỳ SKU nào khác trong shop).*

### 5.3. Hướng dẫn cập nhật phía Frontend (`src/features/seller/SellerScreens.tsx`)
1. **Bỏ bắt buộc nhập SKU**:
   - Ở form tạo biến thể, bỏ dấu hoa thị bắt buộc `*` tại nhãn "Mã SKU".
   - Placeholder có thể đổi thành: `"Tùy chọn: Để trống để hệ thống tự tạo mã duy nhất"`.
2. **Nút "+" (Gợi ý mã SKU)**:
   - Thay vì gọi hàm `buildAutoSku(...)` bằng JavaScript nội bộ của FE, FE có thể gọi:
     ```typescript
     const res = await api.get<{ skuCode: string }>(
       `/api/v1/seller/products/suggest-sku?name=${encodeURIComponent(name)}&variantName=${encodeURIComponent(variant.name)}`
     );
     updateVariantField(vIdx, "skuCode", res.skuCode);
     ```
3. **Payload khi Submit form**:
   - Nếu người dùng không nhập mã SKU, FE có thể gửi `skuCode: ""` hoặc bỏ qua trường này, Backend sẽ tự sinh và trả về mã SKU hoàn chỉnh trong đối tượng variant được tạo.

---

Nếu FE team có bất kỳ thắc mắc hoặc cần hỗ trợ thêm, vui lòng liên hệ Backend Catalog team.
