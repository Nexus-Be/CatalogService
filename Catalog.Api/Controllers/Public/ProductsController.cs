namespace Catalog.Api.Controllers.Public;

using System;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Api.Filters;
using Catalog.Application.Common.Models;
using Catalog.Application.Services.Product;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>Lists active products with pagination.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedData<ProductDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var products = await _productService.ListAsync(page, pageSize, ct);
        return Ok(products);
    }

    /// <summary>Gets product details by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var product = await _productService.GetByIdAsync(id, ct);
        return Ok(product);
    }

    /// <summary>Creates a new product and initializes warehouse inventory.</summary>
    [HttpPost]
    [RequireRole("seller", "admin")]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var product = await _productService.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, product);
    }
}
