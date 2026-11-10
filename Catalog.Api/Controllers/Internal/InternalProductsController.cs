namespace Catalog.Api.Controllers.Internal;

using System;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Application.Services.Product;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/internal/products")]
[Produces("application/json")]
public class InternalProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public InternalProductsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>Kiểm tra tiền điều kiện sản phẩm trước khi tạo/mở phiên đấu giá.</summary>
    [HttpGet("{id:guid}/auction-eligibility")]
    [ProducesResponseType(typeof(AuctionEligibilityResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuctionEligibilityResponse>> CheckAuctionEligibility(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var result = await _productService.CheckAuctionEligibilityAsync(id, ct);
        return Ok(result);
    }

    /// <summary>Khóa sản phẩm khi phiên đấu giá bắt đầu mở (Status -> InAuction).</summary>
    [HttpPost("{id:guid}/lock-auction")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> LockForAuction(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var result = await _productService.LockForAuctionAsync(id, ct);
        return Ok(result);
    }

    /// <summary>Mở khóa sản phẩm khi phiên bị hủy hoặc không ai mua (Status -> Active).</summary>
    [HttpPost("{id:guid}/unlock-auction")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> UnlockFromAuction(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var result = await _productService.UnlockFromAuctionAsync(id, ct);
        return Ok(result);
    }

    /// <summary>Chốt bán sản phẩm khi phiên thành công (Status -> Auctioned).</summary>
    [HttpPost("{id:guid}/mark-auctioned")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> MarkAsAuctioned(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var result = await _productService.MarkAsAuctionedAsync(id, ct);
        return Ok(result);
    }

    /// <summary>Hoàn tác sản phẩm đấu giá về Active khi Seller từ chối đơn hoặc hủy giao dịch sau thanh toán (Status -> Active).</summary>
    [HttpPost("{id:guid}/revert-auctioned")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> RevertAuctioned(
        [FromRoute] Guid id,
        [FromBody] RevertAuctionedRequest? request,
        CancellationToken ct)
    {
        var result = await _productService.RevertAuctionedAsync(id, request, ct);
        return Ok(result);
    }
}

