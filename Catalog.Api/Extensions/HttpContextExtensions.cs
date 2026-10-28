namespace Catalog.Api.Extensions;

using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

public static class HttpContextExtensions
{
    public static Guid? GetCurrentSellerId(this HttpContext? context, Guid? fallback = null)
    {
        if (context == null) return fallback;

        // 1. Try header X-Seller-Id
        if (context.Request.Headers.TryGetValue("X-Seller-Id", out var sellerIdHeader) &&
            Guid.TryParse(sellerIdHeader.FirstOrDefault(), out var sellerId) &&
            sellerId != Guid.Empty)
        {
            return sellerId;
        }

        // 2. Try header X-User-Id
        if (context.Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) &&
            Guid.TryParse(userIdHeader.FirstOrDefault(), out var userId) &&
            userId != Guid.Empty)
        {
            return userId;
        }

        // 3. Try claims
        var claimId = context.User?.FindFirst("seller_id")?.Value
            ?? context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User?.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(claimId) && Guid.TryParse(claimId, out var claimGuid) && claimGuid != Guid.Empty)
        {
            return claimGuid;
        }

        // 4. Try query string (?sellerId=...)
        if (context.Request.Query.TryGetValue("sellerId", out var querySellerId) &&
            Guid.TryParse(querySellerId.FirstOrDefault(), out var queryGuid) &&
            queryGuid != Guid.Empty)
        {
            return queryGuid;
        }

        return fallback;
    }
}

