# CATALOG SERVICE - GET /api/v1/seller/products/suggest-sku

> **Feature / Use Case:** Gợi ý mã SKU duy nhất và chuẩn hóa cho biến thể sản phẩm của Người bán [CAT-69] (Đối soát trực tiếp database, chống trùng lặp tuyệt đối trong shop)  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày cập nhật:** 2026-11-28  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/seller/products/suggest-sku`
- **Quy tắc Bán Hàng & Định danh Người Bán:**
  - Header bắt buộc: `X-User-Id` (UUID của Seller/Admin) và `X-User-Roles` (`seller` hoặc `admin`).
  - Hệ thống tự động trích xuất `currentUserId` từ `HttpContext.GetUserId()`. Mã SKU được sinh ra và kiểm tra đảm bảo **tính duy nhất tuyệt đối trong phạm vi shop của chính người bán đó**.
- **Quyền hạn (RBAC Policy):** `[RequireRole("seller", "admin")]`
- **Controller:** `Catalog.Api/Controllers/Public/SellerProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs` (`SuggestSkuAsync`)

---

## 2. Đầu vào & Tham số Truy vấn (Input & Query Parameters)

### 2.1 Query Parameters
| Tham số | Kiểu C# | Bắt buộc | Mặc định | Giới hạn / Ràng buộc | Mô tả |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `name` | `string?` | Không | `null` | Max 255 ký tự | Tên sản phẩm chính (ví dụ: `Gối ôm công thái học`, `Áo thun nam Polo`). |
| `variantName` | `string?` | Không | `null` | Max 255 ký tự | Tên của biến thể cụ thể (ví dụ: `Màu xám`, `Size XL`, `Đen - L`). |

---

## 3. Quy chuẩn Thuật toán Sinh mã SKU (SKU Generation Algorithm)

Backend CatalogService thực thi quy trình chuẩn hóa và sinh mã an toàn:
1. **Khử dấu tiếng Việt & Ký tự đặc biệt (Diacritic Stripping & Normalization):**
   - Chuyển toàn bộ ký tự tiếng Việt có dấu thành không dấu (ví dụ: `Gối ôm` $\to$ `GOI OM`, `Đen` $\to$ `DEN`).
   - Loại bỏ mọi ký tự không phải chữ cái hoặc số, thay thế khoảng trắng thành dấu gạch ngang (`-`).
2. **Cắt lọc độ dài định danh (Slug Truncation):**
   - Lấy tối đa **3 từ đầu tiên** của tên sản phẩm.
   - Lấy tối đa **2 từ đầu tiên** của tên biến thể.
   - Nếu cả 2 tham số đều để trống hoặc không hợp lệ, hệ thống sử dụng tiền tố mặc định `PROD`.
3. **Thêm Hậu tố Ngẫu nhiên An toàn (Unique 4-char Hex Suffix):**
   - Nối thêm hậu tố 4 ký tự Hex ngẫu nhiên (ví dụ: `BF78`, `4A2C`).
   - Kết quả dạng: `[TEN-SP]-[BIEN-THE]-[HAU-TO]` (ví dụ: `GOI-OM-CONG-MAU-XAM-BF78`).
4. **Đối soát Cơ sở dữ liệu (Shop-Level Isolation & Collision Resistance):**
   - Truy vấn trực tiếp vào bảng `ProductVariants` kết nối qua `Products.SellerId == currentUserId`.
   - Nếu phát hiện mã SKU đã tồn tại trong gian hàng của người bán đó, hệ thống tự động sinh lại hậu tố ngẫu nhiên khác trong vòng lặp an toàn tối đa 10 lần cho đến khi đảm bảo **duy nhất 100%**.

---

## 4. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `200 OK`
- **Kiểu trả về:** `SuggestSkuResponse`
- **Khai báo C# Record:**
```csharp
namespace Catalog.Application.Services.Product;

public sealed record SuggestSkuResponse(string SkuCode);
```

- **Ví dụ JSON Response Body:**
```json
{
  "skuCode": "GOI-OM-CONG-MAU-XAM-BF78"
}
```

---

## 5. Bảng Ma trận Mã Lỗi (RFC 9110 Error Matrix)

| HTTP Status | Error Code | Điều kiện kích hoạt | Error Message chuẩn |
| :---: | :--- | :--- | :--- |
| `401 Unauthorized` | `UNAUTHORIZED` | Thiếu token xác thực hoặc không có header `X-User-Id`. | `"User context is missing or unauthenticated."` |
| `403 Forbidden` | `FORBIDDEN` | Người dùng không mang vai trò `seller` hoặc `admin`. | `"You do not have permission to access this resource."` |

---

## 6. Hướng dẫn Tích hợp Frontend & cURL Mẫu

### 6.1. cURL Mẫu
```bash
curl -X GET "http://localhost:8002/api/v1/seller/products/suggest-sku?name=G%E1%BB%91i%20%C3%B4m%20c%C3%B4ng%20th%C3%A1i%20h%E1%BB%8Dc&variantName=M%C3%A0u%20x%C3%A1m" \
  -H "X-User-Id: 216c5b96-6df0-4a81-8051-2487ee3b4e76" \
  -H "X-User-Roles: seller"
```

### 6.2. Mẫu tích hợp React / TypeScript (Seller Form)
```typescript
// Gợi ý SKU khi người bán bấm nút Tạo SKU cạnh ô nhập
async function onSuggestSkuClicked(productName: string, variantName: string) {
  const query = new URLSearchParams();
  if (productName) query.append("name", productName);
  if (variantName) query.append("variantName", variantName);

  const res = await fetch(`/api/v1/seller/products/suggest-sku?${query.toString()}`, {
    headers: {
      "Authorization": `Bearer ${token}`
    }
  });
  
  if (res.ok) {
    const data: { skuCode: string } = await res.json();
    // Cập nhật giá trị vào state của form
    setVariantSku(data.skuCode);
  }
}
```
