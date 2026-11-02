namespace Catalog.Api.Controllers.Internal;

using System.Threading;
using System.Threading.Tasks;
using Catalog.Application.Services.Product;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/internal/variants")]
[Produces("application/json")]
public class InternalVariantsController : ControllerBase
{
    private readonly IProductService _productService;

    public InternalVariantsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>Gets single variant details by ID (CAT-21). Used by Cart service.</summary>
    [HttpGet("{id:guid}")]
    [HttpGet("/api/v1/internal/catalog/skus/{id:guid}")]
    [ProducesResponseType(typeof(VariantDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VariantDetailDto>> GetVariantById(
        Guid id,
        CancellationToken ct)
    {
        var response = await _productService.GetVariantByIdAsync(id, ct);
        return Ok(response);
    }

    /// <summary>Batch gets variant details by a list of IDs (CAT-24). Used by Cart and Order services.</summary>
    [HttpPost]
    [HttpPost("/api/v1/internal/catalog/skus:batch-get")]
    [ProducesResponseType(typeof(BatchVariantResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BatchVariantResponse>> GetVariantsBatch(
        [FromBody] BatchGetVariantsRequest request,
        CancellationToken ct)
    {
        var response = await _productService.GetVariantsBatchAsync(request, ct);
        return Ok(response);
    }

    /// <summary>Checks purchasability status for a batch of variants (CAT-39). Used by Cart and Checkout flows.</summary>
    [HttpPost("check-status")]
    [ProducesResponseType(typeof(CheckVariantStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CheckVariantStatusResponse>> CheckVariantStatusBatch(
        [FromBody] CheckVariantStatusRequest request,
        CancellationToken ct)
    {
        var response = await _productService.CheckVariantStatusBatchAsync(request, ct);
        return Ok(response);
    }

    /// <summary>Validates switching between variants within the same product (CAT-47). Used by Cart service.</summary>
    [HttpPost("validate-switch")]
    [HttpPost("/api/v1/internal/catalog/skus/validate-variant-switch")]
    [ProducesResponseType(typeof(ValidateVariantSwitchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ValidateVariantSwitchResponse>> ValidateVariantSwitch(
        [FromBody] ValidateVariantSwitchRequest request,
        CancellationToken ct)
    {
        var response = await _productService.ValidateVariantSwitchAsync(request, ct);
        return Ok(response);
    }
}