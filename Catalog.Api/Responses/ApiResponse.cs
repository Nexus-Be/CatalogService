namespace Catalog.Api.Responses;

using System;
using System.Collections.Generic;

public class ApiResponse<T>
{
    public bool Success { get; set; } = true;
    public int StatusCode { get; set; } = 200;
    public string? Message { get; set; }
    public T? Data { get; set; }
    public ApiError[]? Errors { get; set; }
    public string? TraceId { get; set; }
    public string Timestamp { get; set; } = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
}

public class PagedData<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public PaginationMetadata Pagination { get; set; } = new();
}

public class PaginationMetadata
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }

    public static PaginationMetadata Create(int pageIndex, int pageSize, int totalCount)
    {
        var totalPages = pageSize > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0;
        return new PaginationMetadata
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasPreviousPage = pageIndex > 1,
            HasNextPage = pageIndex < totalPages
        };
    }
}

public class ApiError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ApiErrorResponse
{
    public string Type { get; set; } = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    public string Title { get; set; } = "One or more errors occurred.";
    public int Status { get; set; } = 400;
    public string? ErrorCode { get; set; }
    public string? Detail { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }
    public string? TraceId { get; set; }
    public string Timestamp { get; set; } = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
}
