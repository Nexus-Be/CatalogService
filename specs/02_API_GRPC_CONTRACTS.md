# 02 — HỢP ĐỒNG GIAO TIẾP GRPC (CATALOG SERVICE GRPC CONTRACTS)

Dịch vụ gRPC của CatalogService được định nghĩa trong hợp đồng duy nhất `Contracts/shop.proto` dưới service `CatalogApi`:

---

## 1. DANH SÁCH CÁC RPC METHODS

```protobuf
service CatalogApi {
  // Danh mục sản phẩm (Categories)
  rpc ListCategories(PageRequest) returns (CategoryPage);
  rpc GetCategory(IdRequest) returns (Category);
  rpc CreateCategory(CategoryInput) returns (Category);
  rpc UpdateCategory(UpdateCategoryRequest) returns (Category);
  rpc DeleteCategory(IdRequest) returns (DeleteResponse);
  
  // Sản phẩm công khai (Products)
  rpc ListProducts(ProductQuery) returns (ProductPage);
  rpc GetProduct(IdRequest) returns (Product);
  
  // Sản phẩm của người bán (Seller Products)
  rpc ListProductsBySeller(ProductQuery) returns (ProductPage);
  rpc CreateProduct(ProductInput) returns (Product);
  rpc UpdateProduct(UpdateProductRequest) returns (Product);
  rpc DeleteProduct(IdRequest) returns (DeleteResponse);
  
  // Kho hàng ảo (Inventory)
  rpc GetInventory(UserRequest) returns (InventoryPage);
  rpc AdjustInventory(AdjustInventoryRequest) returns (Inventory);
}
```

---

## 2. QUY CHUẨN XỬ LÝ LỖI & MÃ LỖI (ERROR STATUS CODES)

Khi xử lý trong `CatalogGrpcService`, ném ra `RpcException`:
- **`StatusCode.InvalidArgument`** (400 Bad Request): Giá sản phẩm $\le 0$, tên sản phẩm rỗng, `Available < 0`, `CategoryId` không đúng định dạng UUID.
- **`StatusCode.NotFound`** (404 Not Found): Không tìm thấy sản phẩm hoặc danh mục với ID yêu cầu.
- **`StatusCode.PermissionDenied`** (403 Forbidden): Seller sửa hoặc xóa sản phẩm thuộc quyền sở hữu của seller khác.
- **`StatusCode.AlreadyExists`** (409 Conflict): Slug sản phẩm bị trùng lặp trong hệ thống.
- **`StatusCode.FailedPrecondition`** (412 Precondition Failed): Xóa danh mục khi danh mục đó vẫn còn chứa sản phẩm con hoặc danh mục con.
