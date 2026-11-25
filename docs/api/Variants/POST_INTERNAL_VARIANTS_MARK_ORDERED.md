# POST /api/v1/internal/variants/mark-ordered

## 1. Overview
Internal service-to-service endpoint used by CommerceService when an order is confirmed and paid, marking variants as having order history (`HasOrder = true`) so they cannot be deleted.

- **Method**: `POST`
- **Path**: `/api/v1/internal/variants/mark-ordered`
- **Alias**: `/api/v1/internal/catalog/skus/mark-ordered`
- **Authentication**: Service-to-Service Secret Header (`X-Internal-Service-Key` or `x-service-key`)

## 2. Request Headers
| Header | Type | Required | Description |
|---|---|---|---|
| `X-Internal-Service-Key` | string | Yes | Internal shared security key |
| `Content-Type` | string | Yes | `application/json` |

## 3. Request Body
`MarkVariantsOrderedRequest`:
```json
{
  "skuIds": [
    "d586c18b-6998-4738-95a2-e9b15ea9607b"
  ]
}
```

## 4. Response
### HTTP 200 OK
Returns `MarkOrderedResponse`:
```json
{
  "markedCount": 1,
  "updatedSkuIds": [
    "d586c18b-6998-4738-95a2-e9b15ea9607b"
  ]
}
```

### HTTP 401 Unauthorized
Returned if `X-Internal-Service-Key` is missing or invalid.
