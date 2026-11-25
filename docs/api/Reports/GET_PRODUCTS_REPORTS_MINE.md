# GET /api/v1/products/reports/mine

## 1. Overview
Returns the list of product violation reports submitted by the currently authenticated user (Buyer or Seller).

- **Method**: `GET`
- **Path**: `/api/v1/products/reports/mine`
- **Authentication**: Bearer JWT (`x-user-id`, `x-user-roles`)
- **Required Roles**: `buyer`, `seller`

## 2. Request Headers
| Header | Type | Required | Description |
|---|---|---|---|
| `Authorization` | string | Yes | Bearer token |
| `x-user-id` | GUID | Yes | Authenticated user ID |
| `x-user-roles` | string | Yes | Roles of user (`buyer`, `seller`) |

## 3. Query Parameters
None.

## 4. Response
### HTTP 200 OK
Returns an array of `ProductReportDto`:
```json
[
  {
    "id": "7b8f9e12-3456-4789-a012-3456789abcde",
    "productId": "f7b58d9b-1078-4d16-93db-ec194994dc20",
    "reporterId": "a3db35fe-6ca2-4568-aa9e-671208753d79",
    "reason": "COUNTERFEIT",
    "description": "The item received is a counterfeit copy of the sunny brand.",
    "evidenceImages": [
      "https://res.cloudinary.com/u6yxkkop/image/upload/v1794901987/nexus_catalog/reports/evidence1.jpg"
    ],
    "status": "Pending",
    "resolutionNote": null,
    "reviewedBy": null,
    "reviewedAt": null,
    "createdAt": "2026-11-20T10:00:00Z",
    "updatedAt": "2026-11-20T10:00:00Z"
  }
]
```

### HTTP 401 Unauthorized
```json
{
  "errorCode": "UNAUTHORIZED",
  "detail": "User is not authenticated."
}
```

### HTTP 403 Forbidden
```json
{
  "errorCode": "FORBIDDEN",
  "detail": "Required role 'buyer' or 'seller' is missing."
}
```
