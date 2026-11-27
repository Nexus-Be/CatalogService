# TÀI LIỆU YÊU CẦU KHẮC PHỤC LỖ HỔNG BẢO VỆ KHO HÀNG (DEFECT HANDOFF REPORT)
## V/v: Chặn Tuyệt Đối Nhập Kho Hàng Cho Sản Phẩm Chưa Được Staff Phê Duyệt

> **Người gửi:** Catalog Service Backend Engineering Team (Port `8002`)  
> **Người nhận 1:** Frontend Engineering Team (`Nexus Web / Seller Center`)  
> **Người nhận 2:** Fulfillment Service Engineering Team (Port `8006` / `fulfillment_db`)  
> **Đồng kính gửi:** Ban Quản Trị Hệ Thống Nexus, Commerce Service Team  
> **Mã lỗi theo dõi:** `FE-INV-21` & `BE-FULFILL-01`  
> **Mức độ nghiêm trọng:** 🔴 **CRITICAL (Lỗ hổng logic nghiệp vụ cốt lõi)**  
> **Ngày lập:** 27/11/2026  
> **Trạng thái:** 🟡 ĐANG CHỜ KHẮC PHỤC (Pending Resolution)  

---

## 1. TÓM TẮT VẤN ĐỀ & MỨC ĐỘ ẢNH HƯỞNG

### 1.1. Hiện tượng thực tế (Bug Behavior)
Trong quy trình vận hành của nền tảng, một sản phẩm chỉ được phép mở bán và nhận hàng vào kho khi đã được nhân viên kiểm duyệt sàn (Staff/Admin) phê duyệt (`ProductStatus == Active`). 

Tuy nhiên, hiện tại trong hệ thống đang tồn tại lỗ hổng cho phép **Người bán (Seller) tự ý nhập hàng tồn kho thành công cho cả các sản phẩm đang ở bản nháp (`Draft`) hoặc đang chờ duyệt (`PendingApproval`)**, thậm chí cả sản phẩm đã bị từ chối duyệt (`Rejected`).

### 1.2. Hậu quả & Rủi ro nghiệp vụ
1. **Phá vỡ quy trình kiểm duyệt chất lượng sàn:** Hàng hóa giả, hàng cấm, hoặc thông tin chưa được kiểm duyệt vẫn được đưa vào kho vật lý và ghi nhận số lượng tồn kho khả dụng.
2. **Sai lệch dữ liệu liên dịch vụ:** CatalogService coi sản phẩm là chưa được duyệt (chưa khai báo SKU vào kho), nhưng bên kho của FulfillmentService lại tự động phát sinh bản ghi tồn kho và lịch sử nhập kho (`InventoryLedger`).
3. **Nguy cơ lỗi phát sinh khi giao vận GHN:** Sản phẩm nháp có thể chưa hoàn thiện thông số kích thước đóng gói (`WeightGrams`, `LengthCm`, `WidthCm`, `HeightCm`), dẫn đến thất bại khi tạo vận đơn giao hàng GHN sau này.

---

## 2. PHÂN TÍCH NGUYÊN NHÂN GỐC RỄ (ROOT CAUSE ANALYSIS)

Lỗ hổng này xuất phát từ sự thiếu sót trong việc kiểm soát điều kiện ở **cả 2 tầng: Giao diện người dùng (Frontend) và Dịch vụ kho (FulfillmentService)**:

### 🚨 2.1. Lỗ hổng phía Frontend (`Nexus`)
* **File vi phạm:** [`Nexus/src/features/seller/SellerScreens.tsx`](file:///d:/Apartment/FALL_26/FSA/Project/Nexus/src/features/seller/SellerScreens.tsx)
* **Vị trí 1 (Dòng 1488 – 1515):** Màn hình Quản lý kho (`/seller/inventory`) gọi `sellerCatalogService.listAllProductDetails()`. Hàm này lấy tất cả sản phẩm của shop mà **không lọc bỏ các sản phẩm chưa `Active`**. Kết quả là các sản phẩm `Draft` và `PendingApproval` đều bị đưa vào bảng quản lý kho.
* **Vị trí 2 (Dòng 1920 – 1928):** Nút **`+ Nhập hàng`** (`openIntakeModal`) trong từng dòng biến thể:
  ```tsx
  // Nexus/src/features/seller/SellerScreens.tsx:1921-1926
  <button
    type="button"
    className="button button-small button-primary"
    disabled={!isOwnWarehouse || batchBalances.loading} // ❌ KHÔNG KIỂM TRA TRẠNG THÁI SẢN PHẨM
    onClick={() => openIntakeModal(variant.variantId)}
  >
    + Nhập hàng
  </button>
  ```
  👉 **Lỗi:** Nút bấm chỉ kiểm tra `isOwnWarehouse`, hoàn toàn **KHÔNG kiểm tra `product.status === "ACTIVE"`**. Do đó, nút luôn sáng và Seller thoải mái bấm mở modal nhập hàng.

---

### 🚨 2.2. Lỗ hổng phía Backend Fulfillment Service (`FulfillmentService`)
* **File vi phạm:** [`FulfillmentService/Fulfillment.Application/Services/Inventory/InventoryAppService.Items.cs`](file:///d:/Apartment/FALL_26/FSA/Project/FulfillmentService/Fulfillment.Application/Services/Inventory/InventoryAppService.Items.cs)
* **Vị trí:** Phương thức `ApplyAdjustment` (Dòng 245 – 260):
  ```csharp
  // Fulfillment.Application/Services/Inventory/InventoryAppService.Items.cs:250-260
  var item = existingItem;
  if (item == null)
  {
      // ❌ LỖ HỔNG NGHIỆM TRỌNG:
      // SKU chưa từng được CatalogService khai báo/khởi tạo vào kho (do chưa được Staff duyệt),
      // nhưng nếu có yêu cầu Intake thì code TỰ ĐỘNG TẠO MỚI ITEM TRONG KHO!
      if (warehouse != null && type == InventoryAdjustmentType.Intake && quantity > 0)
      {
          item = InventoryItem.Create(skuId, warehouse.Id, 0); 
          _dbContext.InventoryItems.Add(item);
      }
      else
      {
          throw new EntityNotFoundException("INVENTORY_NOT_FOUND", "InventoryItem for SKU", skuId);
      }
  }

  item.Adjust(quantity); // Tăng tồn kho và tạo bản ghi Ledger
  ```
  👉 **Lỗi:** `FulfillmentService` **tự ý sinh ra bản ghi kho** cho bất kỳ SKU nào được gửi lên trong request `Intake` mà **hoàn toàn không xác minh lại với CatalogService** xem sản phẩm có đang `Active` hay không.

---

## 3. HƯỚNG DẪN KHẮC PHỤC CHI TIẾT (ACTIONABLE FIXES)

---

### 💻 3.1. DÀNH CHO FRONTEND TEAM (`Nexus`)

#### Nhiệm vụ 1: Khóa nút `+ Nhập hàng` khi sản phẩm chưa được duyệt
Tại `Nexus/src/features/seller/SellerScreens.tsx` (dòng 1924):
* **Cũ:**
  ```tsx
  disabled={!isOwnWarehouse || batchBalances.loading}
  ```
* **Sửa thành:**
  ```tsx
  disabled={
    !isOwnWarehouse || 
    batchBalances.loading || 
    product.status.toUpperCase() !== "ACTIVE"
  }
  title={
    product.status.toUpperCase() !== "ACTIVE" 
      ? "Sản phẩm cần được Staff duyệt (Active) trước khi có thể nhập hàng vào kho." 
      : undefined
  }
  ```

#### Nhiệm vụ 2: Bổ sung cảnh báo trực quan trong bảng kho
* Tại cột trạng thái sản phẩm trong bảng kho (dòng 1895), nếu `product.status !== "ACTIVE"`, hiển thị nhãn cảnh báo:
  ```tsx
  {product.status.toUpperCase() !== "ACTIVE" && (
    <span className="badge badge-warning" style={{ fontSize: 11, marginLeft: 6 }}>
      Chưa thể nhập hàng (Chờ duyệt)
    </span>
  )}
  ```

#### Nhiệm vụ 3: Chặn trực tiếp tại hàm submit `submitIntake`
Tại hàm `submitIntake` (dòng 1680), thêm bước kiểm tra an toàn trước khi gọi API:
```typescript
if (activeRow?.product.status.toUpperCase() !== "ACTIVE") {
  setIntakeError("Sản phẩm chưa được Staff phê duyệt. Không thể nhập hàng vào kho.");
  return;
}
```

---

### 📦 3.2. DÀNH CHO FULFILLMENT SERVICE TEAM (Port `8006`)

Backend FulfillmentService là chốt chặn bảo mật cuối cùng để bảo vệ tính toàn vẹn dữ liệu kho. Fulfillment không được dựa dẫm vào Frontend!

#### Phương án A (Khuyên dùng - Khắt khe & Chuẩn kiến trúc):
Bỏ cơ chế tự động tạo mới `InventoryItem.Create` trong `ApplyAdjustment` khi `existingItem == null`:
1. **Nguyên tắc:** SKU **BẮT BUỘC** phải được CatalogService khai báo trước qua endpoint nội bộ:
   `POST api/v1/internal/inventories/initialize` (khi Admin duyệt `Active`).
2. Nếu `existingItem == null`, ném ngay ngoại lệ:
   ```csharp
   if (item == null)
   {
       throw new BusinessRuleValidationException(
           "SKU_NOT_INITIALIZED", 
           $"SKU '{skuId}' has not been declared or approved by CatalogService.");
   }
   ```

#### Phương án B (Thẩm định S2S trước khi cho phép Intake):
Nếu FulfillmentService vẫn muốn giữ cơ chế tạo linh hoạt, FulfillmentService **BẮT BUỘC PHẢI GỌI S2S** sang CatalogService để kiểm tra trạng thái trước khi tạo:
* **Endpoint kiểm tra của CatalogService (sẵn có):**
  `POST http://nexus-catalog-service:8002/api/v1/internal/variants/check-status`  
  Payload: `{ "skuIds": ["<skuId>"] }`
* **Xử lý:**
  Nếu CatalogService trả về trạng thái không phải `Active` (ví dụ `Draft`, `PendingApproval`, `Banned`), Fulfillment Service **từ chối ngay lập tức** với mã lỗi HTTP 400:
  ```json
  {
    "type": "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
    "title": "Bad Request",
    "status": 400,
    "errorCode": "PRODUCT_NOT_APPROVED_FOR_INVENTORY",
    "detail": "Cannot intake inventory for an unapproved or inactive product."
  }
  ```

---

## 4. KỊCH BẢN KIỂM THỬ NGHIỆM THU (ACCEPTANCE CRITERIA)

Sau khi FE và Fulfillment áp dụng các bản sửa lỗi trên, các kịch bản kiểm thử sau phải thỏa mãn 100%:

| STT | Trạng thái sản phẩm | Hành vi trên UI Frontend (`/seller/inventory`) | Kết quả gọi API Backend (`POST /api/v1/inventories/adjust`) |
| :--- | :--- | :--- | :--- |
| **TC-01** | `Draft` (Bản nháp) | Nút `+ Nhập hàng` bị vô hiệu hóa (disabled). Di chuột vào hiện thông báo yêu cầu gửi duyệt. | Nếu dùng Postman/Curl gọi trực tiếp, Backend trả về `400 Bad Request` (`PRODUCT_NOT_APPROVED_FOR_INVENTORY`). |
| **TC-02** | `PendingApproval` (Chờ duyệt) | Nút `+ Nhập hàng` bị vô hiệu hóa (disabled). Hiển thị nhãn *"Chờ duyệt"*. | Gọi API trực tiếp bị từ chối `400 Bad Request`. |
| **TC-03** | `Active` (Đã duyệt) | Nút `+ Nhập hàng` sáng bình thường, cho phép mở modal và nhập số lượng. | Nhập hàng thành công, tồn kho tăng lên, ghi nhận nhật ký `InventoryLedger` với type `Intake`. |
| **TC-04** | `Banned` / `Inactive` (Đình chỉ / Tắt) | Nút `+ Nhập hàng` bị vô hiệu hóa (disabled). | Gọi API trực tiếp bị từ chối `400 Bad Request`. |

---

## 5. THÔNG TIN LIÊN HỆ HỖ TRỢ
* **Catalog Service Team:** Kênh liên lạc nội bộ Catalog Backend (`CatalogService_Refactor`).
* **Đặc tả nghiệp vụ tham chiếu:** [docs/CROSS_SERVICE_BUSINESS_RULES_AND_RESTRICTIONS.md](file:///d:/Apartment/FALL_26/FSA/Project/CatalogService_Refactor/docs/CROSS_SERVICE_BUSINESS_RULES_AND_RESTRICTIONS.md).
