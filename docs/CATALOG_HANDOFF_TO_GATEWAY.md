# TÀI LIỆU BÀN GIAO ĐỊNH TUYẾN KỸ THUẬT CHO API GATEWAY TEAM

> **Từ:** Catalog Service Backend Engineering Team (Port `8002`)  
> **Gửi tới:** API Gateway Engineering Team (Port `8080` / YARP Reverse Proxy)  
> **Đồng kính gửi:** Frontend Team (`Nexus`), UserService Team, AuctionService Team  
> **Tài liệu:** Yêu cầu đặc tả cấu hình định tuyến (Routing Configuration Specification) & Giải quyết xung đột Route  
> **Ngày lập:** 27/11/2026  
> **Trạng thái:** ✅ SẴN SÀNG BÀN GIAO (CatalogService đã hoàn thiện và kiểm chứng 100% 53 endpoints)  

---

## 1. MỤC ĐÍCH & BỐI CẢNH

Nhằm phân tách rõ ràng trách nhiệm kiến trúc (Separation of Concerns), CatalogService chịu trách nhiệm độc quyền về việc cung cấp các API nghiệp vụ danh mục, sản phẩm, biến thể, hình ảnh, báo cáo vi phạm và đánh giá sản phẩm.

Tài liệu này bàn giao chi tiết cho **API Gateway Team** danh mục các tuyến đường (routes), quy tắc khớp lệnh (pattern matching), thứ tự ưu tiên (`Order`) và cách xử lý các điểm xung đột định tuyến giữa CatalogService và các dịch vụ anh em (`UserService`, `AuctionService`).

---

## 2. CÁC ĐIỂM XUNG ĐỘT ĐỊNH TUYẾN TRỌNG YẾU CẦN GATEWAY XỬ LÝ

### 2.1. Xung đột giữa Đánh giá Sản phẩm (Catalog) và Đánh giá Đối tác (User)
* **Vấn đề thực tế phát sinh (Ticket `FE-STAFF-20`):**
  * Màn hình Staff `/staff/product-reviews` gọi `GET /api/v1/admin/reviews?page=1&pageSize=20`.
  * Cấu hình Gateway cũ trỏ toàn bộ mẫu `/api/v1/admin/reviews/{**catch-all}` sang `UserService:8001`.
  * `UserService` không có endpoint `GET`, chỉ có `PUT .../hide`, dẫn đến trả về mã lỗi **`404 Not Found`**.
* **Nguyên tắc phân định:**
  1. `GET /api/v1/admin/reviews` (Lấy danh sách đánh giá sản phẩm): **BẮT BUỘC ĐỊNH TUYẾN VỀ `catalog-cluster` (`CatalogService:8002`)**.
  2. `PATCH /api/v1/admin/reviews/{id}/moderate` (Ẩn/Hiện đánh giá sản phẩm): **ĐỊNH TUYẾN VỀ `catalog-cluster` (`CatalogService:8002`)**.
  3. `PUT /api/v1/admin/reviews/{reviewId}/hide` (Ẩn đánh giá đối tác người dùng): **ĐỊNH TUYẾN VỀ `user-cluster` (`UserService:8001`)**.

---

### 2.2. Xung đột giữa Đấu giá cá nhân (Auction) và Hồ sơ cá nhân (User)
* **Vấn đề thực tế phát sinh (Ticket `FE-AUCTION-19`):**
  * Gateway có route `user-me-route` bắt chung mẫu `/api/v1/me/{**catch-all}` chuyển về `UserService:8001`.
  * Khi người mua truy cập `/buyer/watchlist` (`GET /api/v1/me/watchlist`) và `/buyer/deposits` (`GET /api/v1/me/deposits`), request bị chuyển sang `UserService` gây lỗi **`404 Not Found`**.
* **Nguyên tắc phân định:**
  * Gateway cần các route ưu tiên cao (`Order: -1`) để bắt các endpoint đấu giá cá nhân chuyển về `auction-cluster` (`AuctionService:8005`):
    - `GET /api/v1/me/watchlist/{**catch-all}`
    - `GET /api/v1/me/deposits/{**catch-all}`
    - `GET /api/v1/me/auctions/{**catch-all}`

---

### 2.3. Xung đột Đánh giá của Người mua (Buyer Reviews)
* `POST /api/v1/products/{id}/reviews`: Tạo đánh giá sản phẩm $\to$ **`CatalogService:8002`**.
* `PUT /api/v1/reviews/{id}` & `DELETE /api/v1/reviews/{id}`: Sửa/Xóa đánh giá sản phẩm $\to$ **`CatalogService:8002`** (Route ưu tiên `Order: -1` với method `PUT`, `DELETE`).
* `POST /api/v1/reviews`: Tạo đánh giá đối tác shop $\to$ **`UserService:8001`**.

---

## 3. ĐẶC TẢ CẤU HÌNH YARP CHO GATEWAY (CATALOG SERVICE ROUTES)

Gateway Team sử dụng đoạn cấu hình JSON mẫu dưới đây trong `appsettings.json` của Gateway:

```json
{
  "ReverseProxy": {
    "Routes": {
      "admin-product-reviews-list-route": {
        "ClusterId": "catalog-cluster",
        "Order": -1,
        "Metadata": {
          "ServiceName": "CatalogService",
          "ServicePort": "8002",
          "Domain": "Admin Product Reviews Moderation List"
        },
        "Match": {
          "Path": "/api/v1/admin/reviews",
          "Methods": [ "GET" ]
        }
      },
      "admin-product-reviews-moderate-route": {
        "ClusterId": "catalog-cluster",
        "Order": -1,
        "Metadata": {
          "ServiceName": "CatalogService",
          "ServicePort": "8002",
          "Domain": "Admin Product Review Moderation"
        },
        "Match": {
          "Path": "/api/v1/admin/reviews/{id}/moderate",
          "Methods": [ "PATCH" ]
        }
      },
      "catalog-review-by-id-route": {
        "ClusterId": "catalog-cluster",
        "Order": -1,
        "Metadata": {
          "ServiceName": "CatalogService",
          "ServicePort": "8002",
          "Domain": "Buyer Product Review Mutation"
        },
        "Match": {
          "Path": "/api/v1/reviews/{id:guid}",
          "Methods": [ "PUT", "DELETE" ]
        }
      },
      "admin-product-reports-route": {
        "ClusterId": "catalog-cluster",
        "Order": -1,
        "Metadata": {
          "ServiceName": "CatalogService",
          "ServicePort": "8002",
          "Domain": "Admin Product Reports"
        },
        "Match": {
          "Path": "/api/v1/admin/reports/products/{**catch-all}"
        }
      },
      "categories-route": {
        "ClusterId": "catalog-cluster",
        "Metadata": {
          "ServiceName": "CatalogService",
          "ServicePort": "8002",
          "Domain": "Categories"
        },
        "Match": {
          "Path": "/api/v1/categories/{**catch-all}"
        }
      },
      "products-route": {
        "ClusterId": "catalog-cluster",
        "Metadata": {
          "ServiceName": "CatalogService",
          "ServicePort": "8002",
          "Domain": "Products"
        },
        "Match": {
          "Path": "/api/v1/products/{**catch-all}"
        }
      },
      "seller-products-route": {
        "ClusterId": "catalog-cluster",
        "Metadata": {
          "ServiceName": "CatalogService",
          "ServicePort": "8002",
          "Domain": "Seller Products"
        },
        "Match": {
          "Path": "/api/v1/seller/products/{**catch-all}"
        }
      },
      "variants-route": {
        "ClusterId": "catalog-cluster",
        "Metadata": {
          "ServiceName": "CatalogService",
          "ServicePort": "8002",
          "Domain": "Product Variants"
        },
        "Match": {
          "Path": "/api/v1/variants/{**catch-all}"
        }
      },
      "images-route": {
        "ClusterId": "catalog-cluster",
        "Metadata": {
          "ServiceName": "CatalogService",
          "ServicePort": "8002",
          "Domain": "Images"
        },
        "Match": {
          "Path": "/api/v1/images/{**catch-all}"
        }
      }
    },
    "Clusters": {
      "catalog-cluster": {
        "Destinations": {
          "catalog-service": {
            "Address": "http://catalog-service:8002"
          }
        }
      }
    }
  }
}
```

---

## 4. YÊU CẦU HEADER & SECURITY GATEWAY CẦN ĐẢM BẢO

1. **Bảo toàn JWT Token:** Gateway cần chuyển tiếp nguyên vẹn Header `Authorization: Bearer <token>` sang CatalogService để các middleware và filter trích xuất an toàn `UserId` và vai trò (`buyer`, `seller`, `staff`, `admin`).
2. **Forwarding Headers:** Đảm bảo chuyển tiếp `X-Forwarded-For` và `X-Forwarded-Proto` phục vụ hệ thống Audit Log ghi nhận chính xác IP người dùng.
3. **Pure DTO Envelope:** CatalogService trả về DTO thuần túy (không bọc `ApiResponse<T>`); Gateway chịu trách nhiệm giữ nguyên hoặc bổ sung metadata chung của sàn khi phản hồi về Client.
