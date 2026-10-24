namespace Catalog.Api.Controllers;

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Catalog.Api.Mock;
using Catalog.Api.Responses;
using Catalog.Application.Services.Product;

public sealed record CreateProductRequest(Guid SellerId, Guid CategoryId, string Name, string? Description, string? Brand);
public sealed record UpdateProductRequest(string Name, string? Description, string? Brand, string Status);

[ApiController]
[Route("api/v1/catalog/products")]
[Produces("application/json")]
public sealed class ProductsController : ControllerBase
{
    private readonly MockCatalogStore _store;
    private readonly IApiResponseFactory _responseFactory;

    public ProductsController(MockCatalogStore store, IApiResponseFactory responseFactory)
    {
        _store = store;
        _responseFactory = responseFactory;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public IResult ListProducts([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? categoryId = null)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Invalid pagination parameters.",
                status: StatusCodes.Status400BadRequest,
                detail: "Page must be greater than or equal to 1, and pageSize must be between 1 and 100.",
                errorCode: "VAL_INVALID_PAGINATION"
            );
        }

        var (items, total) = _store.GetProducts(page, pageSize, categoryId);
        return _responseFactory.PagedResult(HttpContext, items, page, pageSize, total, StatusCodes.Status200OK);
    }

    [HttpGet("seller/{sellerId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductDto>>), StatusCodes.Status200OK)]
    public IResult ListProductsBySeller([FromRoute] Guid sellerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var (items, total) = _store.GetProducts(page, pageSize, sellerId: sellerId);
        return _responseFactory.PagedResult(HttpContext, items, page, pageSize, total, StatusCodes.Status200OK);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public IResult GetProduct([FromRoute] Guid id)
    {
        var product = _store.GetProductById(id);
        if (product == null)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Product not found.",
                status: StatusCodes.Status404NotFound,
                detail: $"Product with ID '{id}' was not found in the system.",
                errorCode: "PRODUCT_NOT_FOUND"
            );
        }

        return _responseFactory.SuccessResult(HttpContext, product, "Product retrieved successfully.");
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status201Created)]
    public IResult CreateProduct([FromBody] CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: "Product name is required.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        var created = _store.CreateProduct(request.SellerId, request.CategoryId, request.Name, request.Description, request.Brand);
        return _responseFactory.SuccessResult(HttpContext, created, "Product created successfully.", StatusCodes.Status201Created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public IResult UpdateProduct([FromRoute] Guid id, [FromBody] UpdateProductRequest request)
    {
        var updated = _store.UpdateProduct(id, request.Name, request.Description, request.Brand, request.Status);
        if (updated == null)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Product not found.",
                status: StatusCodes.Status404NotFound,
                detail: $"Product with ID '{id}' was not found in the system.",
                errorCode: "PRODUCT_NOT_FOUND"
            );
        }

        return _responseFactory.SuccessResult(HttpContext, updated, "Product updated successfully.");
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public IResult DeleteProduct([FromRoute] Guid id)
    {
        var deleted = _store.DeleteProduct(id);
        if (!deleted)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Product not found.",
                status: StatusCodes.Status404NotFound,
                detail: $"Product with ID '{id}' was not found in the system.",
                errorCode: "PRODUCT_NOT_FOUND"
            );
        }

        return _responseFactory.SuccessResult(HttpContext, new { deleted = true, id }, "Product deleted successfully.");
    }
}
