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

    /// <summary>Batch gets variant details by a list of IDs (CAT-24). Used by Cart and Order services.</summary>
    [HttpPost]
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
}

