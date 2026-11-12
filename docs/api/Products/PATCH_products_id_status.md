# CATALOG SERVICE - PATCH /api/v1/products/{id}/status

> **Feature / Use Case:** Thay đổi trạng thái sản phẩm [CAT-03] (DRAFT $\to$ PENDING_APPROVAL $\to$ INACTIVE kèm quy trình kiểm duyệt Admin/Staff và kiểm tra biến thể, hình ảnh, trọng lượng vận chuyển)  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày cập nhật:** 2026-11-12  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PATCH`
- **Route Endpoint:** `/api/v1/products/{id}/status`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** Người bán chỉ được phép thay đổi trạng thái sản phẩm thuộc quyền sở hữu của chính mình (`product.SellerId == currentUserId`). Admin/Staff được phép can thiệp trên toàn bộ sản phẩm.
- **Controller:** `Catalog.Api/Controllers/Public/ProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs` (`ChangeStatusAsync`)
- **Rào chắn Phê duyệt Lên sàn (Approval Guardrail):**
  - **Seller KHÔNG ĐƯỢC TỰ Ý KÍCH HOẠT (`Active`):** Nếu người bán gọi đổi trạng thái sang `Active`, hệ thống từ chối với mã lỗi `403 Forbidden` (`APPROVAL_REQUIRED`).
  - **Quy trình nộp duyệt (`PendingApproval`):** Người bán hoàn thiện thông tin sản phẩm và gửi yêu cầu phê duyệt bằng cách đổi trạng thái sang `PendingApproval`.
  - **Dời luồng khởi tạo tồn kho vật lý:** Việc gọi `IFulfillmentClient.InitializeInventoryForSkuAsync` sang Fulfillment Service **không còn diễn ra tại endpoint này**. Tồn kho chỉ được kích hoạt sau khi Quản trị viên/Nhân viên duyệt đơn (`APPROVE`) tại `PATCH /api/v1/admin/products/{id}/moderate`.

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)

### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Định danh duy nhất của sản phẩm cần thay đổi trạng thái |

### 2.2 Request Body Record DTO
- **Vị trí file:** `Catalog.Application/Services/Product/ProductRequests.cs`
- **Tên C# Record:** `ChangeProductStatusRequest`
- **Khai báo C# Record:**
```csharp
public sealed record ChangeProductStatusRequest(
    string Status,
    string? StatusReason = null);
```
- **Bảng quy chuẩn Validation:**
| Tham số | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :---: | :--- | :--- |
| `status` | `string` | Có | `Draft`, `PendingApproval`, `Active` (chỉ Admin), `Inactive` | Trạng thái mục tiêu cần chuyển đến |
| `statusReason` | `string?` | Không | Max: 500 ký tự | Lý do thay đổi (được tự động reset về `null` khi nộp duyệt `PendingApproval`) |

- **JSON Request Body mẫu (Seller nộp yêu cầu duyệt sản phẩm):**
```json
{
  "status": "PendingApproval"
}
```

- **JSON Request Body mẫu (Seller tạm ngưng bán sản phẩm):**
```json
{
  "status": "Inactive",
  "statusReason": "SellerPaused"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `ProductDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record ProductDto(
    Guid Id,
    Guid SellerId,
    Guid CategoryId,
    string Name,
    string? Description,
    string? Brand,
    string Status,
    int? WeightGrams = null);
```
- **JSON Response Body mẫu (camelCase):**
```json
{
  "id": "f3f47581-6036-4fa1-b9fa-803770308c71",
  "sellerId": "cccccccc-3333-3333-3333-333333333333",
  "categoryId": "250ac1a7-eef4-43f4-9d0d-310755f735c4",
  "name": "Quần Jogger Nexus Streetwear",
  "description": "Quần jogger thể thao cao cấp co giãn 4 chiều.",
  "brand": "Nexus Wear",
  "status": "PendingApproval",
  "weightGrams": 300
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Invalid status value. Allowed values are DRAFT, ACTIVE, INACTIVE, AUCTIONED, PENDING_APPROVAL.` | Chuỗi `status` không hợp lệ |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Cannot submit product for approval without at least one active variant with a valid price.` | Nộp duyệt khi chưa có variant hoạt động có giá hợp lệ |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Cannot submit product for approval without at least one active product image.` | Nộp duyệt khi chưa có ít nhất 1 ảnh bìa hoạt động (`IsActive = true`) |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Cannot submit auction product for approval without valid shipping weight. Product must declare WeightGrams greater than 0.` | Sản phẩm đấu giá nộp duyệt nhưng thiếu trọng lượng vận chuyển |
| `400 Bad Request` | `PRODUCT_LOCKED_BY_ADMIN` | `This product was suspended by administrator and cannot be submitted for approval. Please contact support.` | Sản phẩm đang bị Admin đình chỉ (`AdminLocked`) cố tình nộp duyệt |
| `400 Bad Request` | `PRODUCT_BANNED` | `This product has been permanently banned and cannot change status.` | Sản phẩm đã bị cấm vĩnh viễn |
| `400 Bad Request` | `PRODUCT_IN_AUCTION` | `Product is currently in an active auction and cannot change status.` | Sản phẩm đang trong phiên đấu giá mở |
| `400 Bad Request` | `PRODUCT_ALREADY_AUCTIONED` | `Auctioned products are permanently locked and cannot change status.` | Sản phẩm đấu giá đã chốt phiên thành công |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không gửi kèm Bearer token hợp lệ |
| `403 Forbidden` | `APPROVAL_REQUIRED` | `Sellers cannot directly activate products. Products must be approved by an administrator or staff member before going live. Please set status to PENDING_APPROVAL.` | Người bán cố tình đổi trực tiếp sang `Active` |
| `403 Forbidden` | `PERMISSION_DENIED` | `You do not have permission to modify this product.` | Người bán không sở hữu sản phẩm này |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | `Product not found with the specified identifier.` | Sản phẩm không tồn tại hoặc đã bị xóa mềm |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi hệ thống trong quá trình cập nhật CSDL |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `ProductStatus.Draft` | `0` | `"Draft"` | Bản nháp ban đầu của người bán |
| `ProductStatus.Active` | `1` | `"Active"` | Đang mở bán công khai (chỉ do Admin/Staff duyệt) |
| `ProductStatus.Inactive` | `2` | `"Inactive"` | Tạm ngưng kinh doanh |
| `ProductStatus.Auctioned` | `3` | `"Auctioned"` | Đã bán xong qua sàn đấu giá |
| `ProductStatus.InAuction` | `4` | `"InAuction"` | Đang trong phiên đấu giá trực tuyến |
| `ProductStatus.Banned` | `5` | `"Banned"` | Bị cấm vĩnh viễn do vi phạm nghiêm trọng |
| `ProductStatus.PendingApproval` | `6` | `"PendingApproval"` | Đang chờ Admin/Staff kiểm duyệt trước khi lên sàn |

---

## 5. Quy trình Vận hành Trạng thái (Lifecycle State Flow)

```
[Draft] ──(Seller: PendingApproval)──> [PendingApproval] ──(Admin: APPROVE)──> [Active] ──> Đồng bộ kho Fulfillment
   │                                           │
   │                                           ├──(Admin: REJECT)──> [Draft] (statusReason = "Rejected")
   │                                           │
   └──────<────────────────────────────────────┘
```
