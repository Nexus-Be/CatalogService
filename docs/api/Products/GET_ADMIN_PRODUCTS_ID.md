# GET /api/v1/admin/products/{id}

## 1. Overview
Retrieves full administrative details for a product, regardless of its status (`Draft`, `PendingApproval`, `Active`, `Inactive`, `InAuction`, `Auctioned`, `Banned`).

- **Method**: `GET`
- **Path**: `/api/v1/admin/products/{id}`
- **Authentication**: Bearer JWT (`x-user-id`, `x-user-roles`)
- **Required Roles**: `admin`, `staff`

## 2. Request Headers
| Header | Type | Required | Description |
|---|---|---|---|
| `Authorization` | string | Yes | Bearer token |
| `x-user-id` | GUID | Yes | Authenticated user ID |
| `x-user-roles` | string | Yes | Roles of user (`admin`, `staff`) |

## 3. Path Parameters
| Parameter | Type | Required | Description |
|---|---|---|---|
| `id` | GUID | Yes | Unique identifier of the product |

## 4. Response
### HTTP 200 OK
Returns `ProductDetailDto`:
```json
{
  "productId": "f7b58d9b-1078-4d16-93db-ec194994dc20",
  "sellerId": "cccccccc-3333-3333-3333-333333333333",
  "name": "camera",
  "description": "Camera mini",
  "brand": "sunny",
  "saleType": "Regular",
  "status": "Active",
  "statusReason": null,
  "specifications": [
    {
      "name": "Bảo hành",
      "value": "12 tháng"
    }
  ],
  "publishedAt": "2026-11-17T07:54:00Z",
  "category": {
    "categoryId": "8b215fd3-4aa8-43c4-b1e3-35981294ecf8",
    "name": "điện tử",
    "level": 1
  },
  "variants": [
    {
      "variantId": "d586c18b-6998-4738-95a2-e9b15ea9607b",
      "skuCode": "CAMERA-TIEU-CHUAN-ARI0",
      "name": "Tiêu chuẩn",
      "price": 100000.00,
      "status": "Active",
      "isActive": true,
      "hasOrder": false,
      "weightGrams": 100,
      "lengthCm": 10,
      "widthCm": 10,
      "heightCm": 5,
      "attributes": [
        {
          "attrName": "Màu sắc",
          "attrValue": "đen"
        }
      ],
      "primaryImageUrl": "https://res.cloudinary.com/u6yxkkop/image/upload/v1794901987/nexus_catalog/products/camera.jpg"
    }
  ],
  "images": [
    {
      "imageId": "de8ac95e-ac5c-458b-aa56-f614eb7e1b1a",
      "url": "https://res.cloudinary.com/u6yxkkop/image/upload/v1794901987/nexus_catalog/products/camera.jpg",
      "altText": "camera",
      "isActive": true,
      "sortOrder": 0
    }
  ],
  "rating": {
    "avgRating": 4.5,
    "reviewCount": 10
  },
  "weightGrams": 100,
  "lengthCm": 10,
  "widthCm": 10,
  "heightCm": 5,
  "createdAt": "2026-11-17T07:50:00Z",
  "updatedAt": "2026-11-17T07:54:00Z"
}
```

### HTTP 404 Not Found
```json
{
  "errorCode": "PRODUCT_NOT_FOUND",
  "detail": "Product 'f7b58d9b-1078-4d16-93db-ec194994dc20' was not found."
}
```
