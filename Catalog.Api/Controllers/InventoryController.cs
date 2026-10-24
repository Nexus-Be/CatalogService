namespace Catalog.Api.Controllers;

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Catalog.Api.Mock;
using Catalog.Api.Responses;

public sealed record AdjustInventoryRequest(int QuantityChange);

[ApiController]
[Route("api/v1/catalog/inventory")]
[Produces("application/json")]
public sealed class InventoryController : ControllerBase
{
    private readonly MockCatalogStore _store;
    private readonly IApiResponseFactory _responseFactory;

    public InventoryController(MockCatalogStore store, IApiResponseFactory responseFactory)
    {
        _store = store;
        _responseFactory = responseFactory;
    }

    [HttpGet("{productId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<InventoryDto>), StatusCodes.Status200OK)]
    public IResult GetInventory([FromRoute] Guid productId)
    {
        var inv = _store.GetInventory(productId);
        return _responseFactory.SuccessResult(HttpContext, inv, "Inventory retrieved successfully.");
    }

    [HttpPost("{productId:guid}/adjust")]
    [ProducesResponseType(typeof(ApiResponse<InventoryDto>), StatusCodes.Status200OK)]
    public IResult AdjustInventory([FromRoute] Guid productId, [FromBody] AdjustInventoryRequest request)
    {
        var inv = _store.AdjustInventory(productId, request.QuantityChange);
        return _responseFactory.SuccessResult(HttpContext, inv, "Inventory adjusted successfully.");
    }
}
