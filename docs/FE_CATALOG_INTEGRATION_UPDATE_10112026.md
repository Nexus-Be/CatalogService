# THÔNG BÁO ĐỒNG BỘ & HƯỚNG DẪN TÍCH HỢP CATALOG SERVICE CHO FRONTEND

> **Kính gửi:** Đội ngũ Frontend Developers (Seller Center Web, Buyer App, Admin Portal)  
> **Dịch vụ phụ trách:** **Catalog Service** (Port `8002` - Docker / Port local Kestrel)  
> **Ngày ban hành:** 10/11/2026  
> **Phản hồi chính thức về:** Tài liệu sửa đổi backend ngày 09/11/2026 (`chi-tiet-code-sua-backend-09112026.md`)

---

## 1. TỔNG QUAN THỐNG NHẤT VỀ PHẠM VI CATALOG SERVICE

Sau khi rà soát các đề xuất sửa đổi từ phía Frontend nhằm chuẩn hóa nguyên tắc **"1 Seller – 1 Kho riêng duy nhất"**, Catalog Service thông báo luồng dữ liệu chuẩn và các điểm Frontend cần đồng bộ lại giao diện như sau:

1. 🚫 **BỎ TRƯỜNG `warehouseId`:** Database Catalog Service không quản lý kho vật lý và **hoàn toàn không có cột `WarehouseId`**. Frontend **tuyệt đối không truyền `warehouseId`** lên bất kỳ API nào của Catalog Service.
2. ⚖️ **BẮT BUỘC BỔ SUNG TRƯỜNG `weightGrams` (GHN):** Bản code Frontend sửa hôm 09/11 là bản cũ chưa có tính năng giao vận GHN. Catalog Service đã bổ sung trường khối lượng `weightGrams` trên cả Entity, Request và DTO. Frontend **bắt buộc phải truyền `weightGrams`** khi tạo/sửa sản phẩm.
3. 📦 **VÒNG ĐỜI KHỞI TẠO TỒN KHO:** Catalog **không tạo tồn kho** khi sản phẩm đang ở bản nháp (`Draft`). Khi sản phẩm chuyển sang **`Active`** (mở bán), Catalog sẽ **tự động gọi sang Fulfillment Service để tìm đúng kho riêng duy nhất của Seller** và khởi tạo định danh SKU với số lượng mặc định bằng `0`.

---

## 2. CHI TIẾT 3 ĐIỂM FRONTEND CẦN ĐIỀU CHỈNH TRÊN GIAO DIỆN

### 2.1. Loại bỏ hoàn toàn trường `warehouseId` khỏi Form Sản phẩm / Biến thể
* **Hiện trạng của FE hôm 09/11:** FE thêm `Guid? WarehouseId` vào `CreateVariantRequest` và gửi `warehouseId` khi tạo sản phẩm.
* **Quy chuẩn Backend Catalog:**
  * Catalog Service chỉ quản lý siêu dữ liệu hàng hóa (tên, mô tả, danh mục, giá bán, biến thể, hình ảnh, cân nặng).
  * Việc hàng hóa nằm ở kho nào thuộc thẩm quyền quản trị của `FulfillmentService`.
  * Khi sản phẩm được kích hoạt mở bán (`Active`), Catalog Service sẽ **tự động gọi ngầm** sang Fulfillment Service qua API `GET /api/v1/warehouses/seller/{sellerId}` để lấy đúng ID kho riêng duy nhất của Seller.
  * 👉 **Hành động của FE:** Xóa bỏ trường `warehouseId` khỏi body gửi lên Catalog Service.

---

### 2.2. Bổ sung trường khối lượng vận chuyển GHN: `weightGrams`
* **Ý nghĩa:** Đây là trường dữ liệu bắt buộc phục vụ việc tích hợp với đơn vị vận chuyển **Giao Hàng Nhanh (GHN)** để tính cước phí giao hàng và tạo vận đơn khi khách đặt mua.
* **Định dạng dữ liệu:**
  * Kiểu dữ liệu: Số nguyên (`int`, đơn vị: **Gram**).
  * Ràng buộc: Giá trị phải $> 0$ (Ví dụ: `350` g, `500` g, `1200` g).
* **Các Endpoint bắt buộc truyền `weightGrams`:**
  1. `POST /api/v1/products`: Truyền `weightGrams` ở cấp sản phẩm cha và trong từng phần tử của mảng `variants`.
  2. `POST /api/v1/products/auction`: Bắt buộc truyền `weightGrams`.
  3. `POST /api/v1/products/{id}/variants`: Bắt buộc truyền `weightGrams` khi thêm biến thể mới.
  4. `PUT /api/v1/products/{id}` & `PUT /api/v1/variants/{id}`: Hỗ trợ cập nhật `weightGrams`.

---

### 2.3. Vòng đời sản phẩm và Thời điểm khởi tạo tồn kho
* **Khi bấm "Lưu bản nháp" (`POST /api/v1/products`):**
  * Sản phẩm được lưu với trạng thái `Draft`.
  * Backend **KHÔNG gọi tạo tồn kho** ở bước này để tránh sinh rác SKU trong kho Fulfillment cho những sản phẩm chưa hoàn thiện.
* **Khi bấm "Đăng bán" (`PATCH /api/v1/products/{id}/status` $\to$ `Active`):**
  * Điều kiện kích hoạt: Sản phẩm có ít nhất 1 ảnh hiển thị (`isActive = true`) và ít nhất 1 biến thể có giá $> 0$.
  * Backend Catalog tự động gọi Fulfillment để khai báo SKU vào đúng kho riêng của Seller với số lượng mặc định bằng `0`.
  * 👉 **Frontend KHÔNG chặn nút kích hoạt nếu tồn kho = 0.** Sau khi mở bán, sản phẩm hiển thị trên sàn ở trạng thái *"Tạm hết hàng"*. Seller sẽ vào màn hình Quản lý kho để thực hiện Nhập hàng thực tế (`Intake`).

---

## 3. JSON REQUEST MẪU CHUẨN ĐỂ FRONTEND TÍCH HỢP

### 3.1. Tạo sản phẩm bán lẻ thường (`POST /api/v1/products`)
```json
{
  "name": "Ao Thun Nam Basic Cotton 100%",
  "description": "Ao thun chat lieu cotton thoang mat, co gian 4 chieu.",
  "brand": "Nexus Basics",
  "categoryId": "10000000-0000-0000-0000-000000000001",
  "weightGrams": 250,
  "specifications": [
    { "name": "Chat lieu", "value": "100% Cotton" },
    { "name": "Xuat xu", "value": "Viet Nam" }
  ],
  "variants": [
    {
      "skuCode": "TSHIRT-BLK-M",
      "name": "Ao Thun Den - Size M",
      "price": 189000,
      "currency": "VND",
      "weightGrams": 250,
      "attributes": [
        { "attrName": "Color", "attrValue": "Black" },
        { "attrName": "Size", "attrValue": "M" }
      ]
    },
    {
      "skuCode": "TSHIRT-BLK-L",
      "name": "Ao Thun Den - Size L",
      "price": 189000,
      "currency": "VND",
      "weightGrams": 270,
      "attributes": [
        { "attrName": "Color", "attrValue": "Black" },
        { "attrName": "Size", "attrValue": "L" }
      ]
    }
  ]
}
```
*(Lưu ý: Tuyệt đối không có trường `warehouseId` trong từng biến thể).*

---

### 3.2. Tạo sản phẩm đấu giá (`POST /api/v1/products/auction`)
```json
{
  "name": "Dong Ho Co Dien Vertu Signature S",
  "description": "Dong ho Vertu Signature S che tac thu cong sang trong.",
  "brand": "Vertu",
  "categoryId": "10000000-0000-0000-0000-000000000001",
  "skuCode": "AUC-VERTU-SIG-001",
  "weightGrams": 450,
  "specifications": [
    { "name": "Nam san xuat", "value": "2018" },
    { "name": "Tinh trang", "value": "Like New 99%" }
  ],
  "attributes": [
    { "attrName": "Material", "attrValue": "Titanium & Gold" }
  ]
}
```

---

### 3.3. Thêm biến thể cho sản phẩm đã có (`POST /api/v1/products/{id}/variants`)
```json
{
  "skuCode": "TSHIRT-WHT-XL",
  "name": "Ao Thun Trang - Size XL",
  "price": 199000,
  "currency": "VND",
  "weightGrams": 290,
  "attributes": [
    { "attrName": "Color", "attrValue": "White" },
    { "attrName": "Size", "attrValue": "XL" }
  ]
}
```

---

## 4. TẠI SAO CATALOG SERVICE KHÔNG TỰ SINH KHO GIẢ CHO SELLER?

Trong file sửa đổi ngày 09/11, Frontend có viết logic để Catalog tự gọi tạo kho với địa chỉ:
`shop_name: "Kho Riêng Gian Hàng", pickup_address: "Kho tiêu chuẩn của Gian hàng"` (các trường `province_id`, `district_id`, `ward_code` đều để trống).

Backend Catalog **đã loại bỏ đoạn code này** vì các lý do an toàn hệ thống sau:
1. **Làm gãy tích hợp GHN:** Giao Hàng Nhanh bắt buộc phải có mã Tỉnh/Thành (`ProvinceId`), Quận/Huyện (`DistrictId`), Phường/Xã (`WardCode`) thật của điểm lấy hàng thì mới tính được cước phí và cho shipper đến lấy hàng. Tạo kho với địa chỉ giả sẽ khiến toàn bộ đơn hàng của Seller đó bị GHN từ chối.
2. **Quy trình đúng:** Kho riêng của Seller phải được khởi tạo tự động từ bên **UserService** ngay khi Admin duyệt hồ sơ Seller Profile (UserService truyền đầy đủ Tỉnh/Huyện/Xã thật của Seller sang Fulfillment Service). Catalog chỉ việc đọc ID kho đã tồn tại này để gán SKU.

---

## 5. HƯỚNG DẪN CẬP NHẬT CODE DÀNH CHO FRONTEND DEVELOPERS

Toàn bộ code mới nhất của Catalog Service (kèm Migration database `WeightGrams`, logic tự động đồng bộ kho riêng và 5 API nội bộ đồng bộ với Auction Service) **đã được merge vào nhánh `dev`**.

Frontend vui lòng chạy các lệnh sau để đồng bộ mã nguồn mới nhất:
```bash
git checkout dev
git pull origin dev
```

Nếu có bất kỳ thắc mắc nào trong quá trình kết nối API, vui lòng liên hệ đội ngũ Catalog Service để được hỗ trợ kiểm tra trực tiếp.
