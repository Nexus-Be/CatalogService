namespace Catalog.Application.Services.Product;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Catalog.Application.Interfaces;
using Catalog.Domain.Enums;

public sealed class ProductService : IProductService
{
    private const string DefaultImageUrl = "https://placehold.co/600x600/png?text=Nexus+Product";
    private readonly ICatalogDbContext _dbContext;
    private readonly IImageStorageService _imageStorageService;

    public ProductService(ICatalogDbContext dbContext, IImageStorageService imageStorageService)
    {
        _dbContext = dbContext;
        _imageStorageService = imageStorageService;
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product == null) return null;

        return new ProductDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Brand,
            product.Status.ToString());
    }

    public async Task<IReadOnlyList<ProductDto>> ListAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var products = await _dbContext.Products
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(product => new ProductDto(
                product.Id,
                product.SellerId,
                product.CategoryId,
                product.Name,
                product.Description,
                product.Brand,
                product.Status.ToString()))
            .ToListAsync(cancellationToken);

        return products;
    }

    public async Task<PagedList<ProductListItemDto>> SearchAsync(ProductListQuery query, CancellationToken cancellationToken = default)
    {
        var products = _dbContext.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.Status == ProductStatus.Active);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim().ToLower();
            products = products.Where(p => p.Name.ToLower().Contains(keyword));
        }
        if (query.CategoryId.HasValue) products = products.Where(p => p.CategoryId == query.CategoryId.Value);
        if (query.SellerId.HasValue) products = products.Where(p => p.SellerId == query.SellerId.Value);
        if (query.SaleType.HasValue) products = products.Where(p => p.SaleType == query.SaleType.Value);
        if (!string.IsNullOrWhiteSpace(query.Brand))
        {
            var brand = query.Brand.Trim().ToLower();
            products = products.Where(p => p.Brand != null && p.Brand.ToLower() == brand);
        }
        if (query.MinPrice.HasValue)
        {
            products = products.Where(p => p.Variants.Any(v => !v.IsDeleted && v.Status == VariantStatus.Active && v.Price >= query.MinPrice.Value));
        }
        if (query.MaxPrice.HasValue)
        {
            products = products.Where(p => p.Variants.Any(v => !v.IsDeleted && v.Status == VariantStatus.Active && v.Price <= query.MaxPrice.Value));
        }

        if (query.MinRating.HasValue)
        {
            products = products.Where(p => p.RatingStats != null && p.RatingStats.AvgRating >= query.MinRating.Value);
        }

        var totalCount = await products.CountAsync(cancellationToken);

        IQueryable<Catalog.Domain.Entities.Product> ordered = query.SortBy switch
        {
            "priceAsc" => products
                .OrderBy(p => p.Variants.Where(v => !v.IsDeleted && v.Status == VariantStatus.Active).Min(v => v.Price))
                .ThenBy(p => p.Id),
            "priceDesc" => products
                .OrderByDescending(p => p.Variants.Where(v => !v.IsDeleted && v.Status == VariantStatus.Active).Min(v => v.Price))
                .ThenBy(p => p.Id),
            "rating" => products.OrderByDescending(p => p.RatingStats != null ? p.RatingStats.AvgRating : 0).ThenBy(p => p.Id),
            _ => products.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
        };

        var rows = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Brand,
                p.SaleType,
                p.Status,
                MinPrice = p.Variants.Where(v => !v.IsDeleted && v.Status == VariantStatus.Active).Min(v => v.Price),
                MaxPrice = p.Variants.Where(v => !v.IsDeleted && v.Status == VariantStatus.Active).Max(v => v.Price),
                PrimaryImageUrl = p.Images
                    .Where(i => !i.IsDeleted && i.IsActive)
                    .Select(i => i.Url)
                    .FirstOrDefault(url => !url.Contains("nexus.local")) ?? DefaultImageUrl,
                AvgRating = p.RatingStats != null ? p.RatingStats.AvgRating : null
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new ProductListItemDto(
            r.Id,
            r.Name,
            r.Brand,
            r.SaleType.ToString(),
            r.Status.ToString(),
            r.MinPrice,
            r.MaxPrice,
            r.PrimaryImageUrl,
            r.AvgRating)).ToList();

        return new PagedList<ProductListItemDto>(items, totalCount);
    }

    public async Task<ProductDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Products
            .AsNoTracking()
            .Where(p => p.Id == id && !p.IsDeleted && p.Status == ProductStatus.Active)
            .Select(p => new ProductDetailDto(
                p.Id,
                p.SellerId,
                p.Name,
                p.Description,
                p.Brand,
                p.SaleType.ToString(),
                p.Status.ToString(),
                p.Specifications,
                p.PublishedAt,
                p.Category == null ? null : new ProductDetailCategoryDto(
                    p.Category.Id,
                    p.Category.ParentId,
                    p.Category.Name,
                    p.Category.Level),
                p.Variants.Where(v => !v.IsDeleted).Select(v => new ProductDetailVariantDto(
                    v.Id,
                    v.SkuCode,
                    v.Name,
                    v.Price,
                    v.Status.ToString(),
                    v.IsActive,
                    v.Attributes.Select(a => new ProductVariantAttributeDto(a.AttrName, a.AttrValue)).ToList()
                )).ToList(),
                p.Images.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder).Select(i => new ProductDetailImageDto(
                    i.Id,
                    i.Url.Contains("nexus.local") ? DefaultImageUrl : i.Url,
                    i.AltText,
                    i.IsActive,
                    i.SortOrder,
                    i.VariantId
                )).ToList(),
                new ProductDetailRatingDto(
                    p.RatingStats != null ? p.RatingStats.AvgRating : 0,
                    p.RatingStats != null ? p.RatingStats.ReviewCount : 0
                )
            ));

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<BatchVariantResponse> GetVariantsBatchAsync(BatchGetVariantsRequest request, CancellationToken cancellationToken = default)
    {
        var distinctIds = request.VariantIds?.Distinct().Take(100).ToList() ?? new List<Guid>();
        if (distinctIds.Count == 0)
        {
            return new BatchVariantResponse(Array.Empty<BatchVariantItemDto>(), Array.Empty<Guid>());
        }

        var variants = await _dbContext.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product)
                .ThenInclude(p => p!.Images)
            .Include(v => v.Attributes)
            .Include(v => v.Images)
            .Where(v => distinctIds.Contains(v.Id) && !v.IsDeleted && v.Product != null && !v.Product.IsDeleted)
            .ToListAsync(cancellationToken);

        var foundIds = variants.Select(v => v.Id).ToHashSet();
        var notFoundIds = distinctIds.Where(id => !foundIds.Contains(id)).ToList();

        var items = variants.Select(v =>
        {
            var primaryImage = v.Images
                .Where(i => !i.IsDeleted && i.IsActive)
                .OrderBy(i => i.SortOrder)
                .Select(i => i.Url)
                .FirstOrDefault()
                ?? v.Product?.Images
                    .Where(i => !i.IsDeleted && i.IsActive)
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.Url)
                    .FirstOrDefault();

            var resolvedImageUrl = (!string.IsNullOrEmpty(primaryImage) && !primaryImage.Contains("nexus.local"))
                ? primaryImage
                : DefaultImageUrl;

            var attrs = v.Attributes
                .Select(a => new ProductVariantAttributeDto(a.AttrName, a.AttrValue))
                .ToList();

            return new BatchVariantItemDto(
                VariantId: v.Id,
                ProductId: v.ProductId,
                SkuCode: v.SkuCode,
                VariantName: v.Name,
                ProductName: v.Product?.Name ?? string.Empty,
                Price: v.Price,
                Currency: v.Currency,
                Status: v.Status.ToString(),
                ProductStatus: v.Product?.Status.ToString() ?? string.Empty,
                SellerId: v.Product?.SellerId ?? Guid.Empty,
                SaleType: v.Product?.SaleType.ToString() ?? string.Empty,
                PrimaryImageUrl: resolvedImageUrl,
                Attributes: attrs
            );
        }).ToList();

        return new BatchVariantResponse(items, notFoundIds);
    }

    public async Task<CheckVariantStatusResponse> CheckVariantStatusBatchAsync(CheckVariantStatusRequest request, CancellationToken cancellationToken = default)
    {
        var distinctIds = request.VariantIds?.Distinct().Take(100).ToList() ?? new List<Guid>();
        if (distinctIds.Count == 0)
        {
            return new CheckVariantStatusResponse(Array.Empty<VariantStatusItemDto>(), Array.Empty<Guid>());
        }

        var purpose = (request.Purpose ?? string.Empty).Trim().ToUpperInvariant();

        var variants = await _dbContext.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product)
            .Where(v => distinctIds.Contains(v.Id))
            .ToListAsync(cancellationToken);

        var variantDict = variants.ToDictionary(v => v.Id);
        var items = new List<VariantStatusItemDto>();
        var notFoundIds = new List<Guid>();

        foreach (var id in distinctIds)
        {
            if (!variantDict.TryGetValue(id, out var v))
            {
                notFoundIds.Add(id);
                continue;
            }

            var isPurchasable = true;
            var reason = "OK";

            var prod = v.Product;
            var productStatus = prod?.Status.ToString() ?? "Unknown";
            var productStatusReason = prod?.StatusReason?.ToString();
            var variantStatus = v.Status.ToString();
            var variantStatusReason = v.StatusReason?.ToString();
            var saleType = prod?.SaleType.ToString() ?? "Regular";

            if (v.IsDeleted || (prod != null && prod.IsDeleted))
            {
                isPurchasable = false;
                reason = "DELETED";
            }
            else if (prod == null || prod.Status != ProductStatus.Active)
            {
                isPurchasable = false;
                reason = "PRODUCT_NOT_ACTIVE";
            }
            else if (v.Status != VariantStatus.Active || !v.IsActive)
            {
                isPurchasable = false;
                reason = "VARIANT_NOT_ACTIVE";
            }
            else if (purpose is "CART" or "CHECKOUT" && prod.SaleType != SaleType.Regular)
            {
                isPurchasable = false;
                reason = "NOT_FOR_REGULAR_SALE";
            }
            else if (purpose is "CART" or "CHECKOUT" && (!v.Price.HasValue || v.Price.Value <= 0))
            {
                isPurchasable = false;
                reason = "PRICE_MISSING";
            }
            else if (purpose == "AUCTION" && prod.SaleType != SaleType.Auction)
            {
                isPurchasable = false;
                reason = "NOT_FOR_AUCTION";
            }

            items.Add(new VariantStatusItemDto(
                VariantId: v.Id,
                ProductId: v.ProductId,
                ProductStatus: productStatus,
                ProductStatusReason: productStatusReason,
                VariantStatus: variantStatus,
                VariantStatusReason: variantStatusReason,
                SaleType: saleType,
                IsPurchasable: isPurchasable,
                Reason: reason,
                Price: v.Price,
                Currency: v.Currency
            ));
        }

        return new CheckVariantStatusResponse(items, notFoundIds);
    }

    public async Task<ProductOperationResult<ProductDto>> CreateAsync(CreateProductCommand command, CancellationToken cancellationToken = default)
    {
        var categoryExists = await _dbContext.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Id == command.CategoryId && !c.IsDeleted, cancellationToken);

        if (!categoryExists)
        {
            return new ProductOperationResult<ProductDto>(
                ProductOperationStatus.InvalidCategory,
                ErrorMessage: $"Category with ID '{command.CategoryId}' does not exist.");
        }

        var product = new Catalog.Domain.Entities.Product
        {
            Id = Guid.NewGuid(),
            SellerId = command.SellerId,
            CategoryId = command.CategoryId,
            Name = command.Name.Trim(),
            Description = command.Description?.Trim(),
            Brand = command.Brand?.Trim(),
            SaleType = command.SaleType,
            Status = ProductStatus.Draft,
            Specifications = command.Specifications,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CreatedBy = command.SellerId,
            UpdatedBy = command.SellerId,
            IsDeleted = false
        };

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new ProductDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Brand,
            product.Status.ToString());

        return new ProductOperationResult<ProductDto>(ProductOperationStatus.Success, dto);
    }

    public async Task<ProductOperationResult<ProductDto>> UpdateAsync(UpdateProductCommand command, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == command.ProductId && !p.IsDeleted, cancellationToken);

        if (product == null)
        {
            return new ProductOperationResult<ProductDto>(
                ProductOperationStatus.NotFound,
                ErrorMessage: $"Product with ID '{command.ProductId}' was not found in the system.");
        }

        if (product.SellerId != command.CurrentSellerId)
        {
            return new ProductOperationResult<ProductDto>(
                ProductOperationStatus.Forbidden,
                ErrorMessage: "You do not have permission to modify this product.");
        }

        if (command.CategoryId.HasValue && command.CategoryId.Value != Guid.Empty && command.CategoryId.Value != product.CategoryId)
        {
            var categoryExists = await _dbContext.Categories
                .AsNoTracking()
                .AnyAsync(c => c.Id == command.CategoryId.Value && !c.IsDeleted, cancellationToken);

            if (!categoryExists)
            {
                return new ProductOperationResult<ProductDto>(
                    ProductOperationStatus.InvalidCategory,
                    ErrorMessage: $"Category with ID '{command.CategoryId.Value}' does not exist.");
            }

            product.CategoryId = command.CategoryId.Value;
        }

        if (!string.IsNullOrWhiteSpace(command.Name))
        {
            product.Name = command.Name.Trim();
        }

        if (command.Description != null)
        {
            product.Description = command.Description.Trim();
        }

        if (command.Brand != null)
        {
            product.Brand = command.Brand.Trim();
        }

        if (command.SaleType.HasValue)
        {
            product.SaleType = command.SaleType.Value;
        }

        if (!string.IsNullOrWhiteSpace(command.Status) && Enum.TryParse<ProductStatus>(command.Status, true, out var parsedStatus))
        {
            product.Status = parsedStatus;
        }

        if (command.Specifications != null)
        {
            product.Specifications = command.Specifications;
        }

        product.UpdatedAt = DateTimeOffset.UtcNow;
        product.UpdatedBy = command.CurrentSellerId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new ProductDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Brand,
            product.Status.ToString());

        return new ProductOperationResult<ProductDto>(ProductOperationStatus.Success, dto);
    }

    public async Task<ProductOperationResult<bool>> SoftDeleteAsync(Guid id, Guid currentSellerId, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

        if (product == null)
        {
            return new ProductOperationResult<bool>(
                ProductOperationStatus.NotFound,
                false,
                ErrorMessage: $"Product with ID '{id}' was not found in the system.");
        }

        if (product.SellerId != currentSellerId)
        {
            return new ProductOperationResult<bool>(
                ProductOperationStatus.Forbidden,
                false,
                ErrorMessage: "You do not have permission to delete this product.");
        }

        var now = DateTimeOffset.UtcNow;
        product.IsDeleted = true;
        product.DeletedAt = now;
        product.UpdatedAt = now;
        product.UpdatedBy = currentSellerId;

        foreach (var variant in product.Variants.Where(v => !v.IsDeleted))
        {
            variant.IsDeleted = true;
            variant.DeletedAt = now;
            variant.UpdatedAt = now;
            variant.UpdatedBy = currentSellerId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new ProductOperationResult<bool>(ProductOperationStatus.Success, true);
    }

    public async Task<ProductOperationResult<ProductImageDto>> AddProductImageAsync(
        Guid productId,
        Guid currentSellerId,
        Stream fileStream,
        string fileName,
        Guid? variantId = null,
        string? altText = null,
        short sortOrder = 0,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, cancellationToken);

        if (product == null)
        {
            return new ProductOperationResult<ProductImageDto>(
                ProductOperationStatus.NotFound,
                ErrorMessage: $"Product with ID '{productId}' was not found in the system.");
        }

        if (product.SellerId != currentSellerId)
        {
            return new ProductOperationResult<ProductImageDto>(
                ProductOperationStatus.Forbidden,
                ErrorMessage: "You do not have permission to upload images for this product.");
        }

        if (variantId.HasValue && variantId.Value != Guid.Empty)
        {
            var variantExists = await _dbContext.ProductVariants
                .AsNoTracking()
                .AnyAsync(v => v.Id == variantId.Value && v.ProductId == productId && !v.IsDeleted, cancellationToken);

            if (!variantExists)
            {
                return new ProductOperationResult<ProductImageDto>(
                    ProductOperationStatus.InvalidCategory,
                    ErrorMessage: $"Variant with ID '{variantId.Value}' does not belong to this product.");
            }
        }

        var uploadResult = await _imageStorageService.UploadAsync(fileStream, fileName, "products", cancellationToken);

        var image = new Catalog.Domain.Entities.ProductImage
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            VariantId = variantId,
            Url = uploadResult.Url,
            AltText = altText ?? product.Name,
            IsActive = true,
            SortOrder = sortOrder,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsDeleted = false
        };

        _dbContext.ProductImages.Add(image);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new ProductImageDto(
            image.Id,
            image.ProductId,
            image.VariantId,
            image.Url,
            image.AltText,
            image.IsActive,
            image.SortOrder);

        return new ProductOperationResult<ProductImageDto>(ProductOperationStatus.Success, dto);
    }

    public async Task<ProductOperationResult<bool>> DeleteProductImageAsync(
        Guid productId,
        Guid imageId,
        Guid currentSellerId,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, cancellationToken);

        if (product == null)
        {
            return new ProductOperationResult<bool>(
                ProductOperationStatus.NotFound,
                false,
                ErrorMessage: $"Product with ID '{productId}' was not found in the system.");
        }

        if (product.SellerId != currentSellerId)
        {
            return new ProductOperationResult<bool>(
                ProductOperationStatus.Forbidden,
                false,
                ErrorMessage: "You do not have permission to delete images for this product.");
        }

        var image = await _dbContext.ProductImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId && !i.IsDeleted, cancellationToken);

        if (image == null)
        {
            return new ProductOperationResult<bool>(
                ProductOperationStatus.NotFound,
                false,
                ErrorMessage: $"Image with ID '{imageId}' was not found for this product.");
        }

        image.IsDeleted = true;
        image.DeletedAt = DateTimeOffset.UtcNow;
        image.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new ProductOperationResult<bool>(ProductOperationStatus.Success, true);
    }

    // CAT-07: Add Variant
    public async Task<VariantOperationResult<VariantDetailDto>> AddVariantAsync(
        Guid productId,
        Guid currentSellerId,
        CreateVariantRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, cancellationToken);

        if (product == null)
        {
            return new VariantOperationResult<VariantDetailDto>(
                VariantOperationStatus.NotFound,
                ErrorMessage: $"Product with ID '{productId}' was not found in the system.");
        }

        if (product.SellerId != currentSellerId)
        {
            return new VariantOperationResult<VariantDetailDto>(
                VariantOperationStatus.Forbidden,
                ErrorMessage: "You do not have permission to add variants to this product.");
        }

        if (product.SaleType != SaleType.Regular)
        {
            return new VariantOperationResult<VariantDetailDto>(
                VariantOperationStatus.InvalidOperation,
                ErrorMessage: "Cannot add variants to an auction product. Auction products only support a single fixed variant.");
        }

        // Ràng buộc cuộc họp 28/10: Giới hạn tối đa 10 variants cho 1 product
        var currentVariantCount = product.Variants.Count(v => !v.IsDeleted);
        if (currentVariantCount >= 10)
        {
            return new VariantOperationResult<VariantDetailDto>(
                VariantOperationStatus.InvalidOperation,
                ErrorMessage: "A product cannot exceed 10 variants.");
        }

        var sku = request.SkuCode?.Trim();
        if (string.IsNullOrWhiteSpace(sku))
        {
            return new VariantOperationResult<VariantDetailDto>(
                VariantOperationStatus.InvalidOperation,
                ErrorMessage: "SKU code is required.");
        }

        // Ràng buộc cuộc họp 28/10: SKU được phép trùng giữa các seller khác nhau,
        // nhưng TRONG NỘI BỘ SHOP CỦA 1 SELLER ĐÓ thì KHÔNG ĐƯỢC TRÙNG SKU.
        var skuExists = await _dbContext.ProductVariants
            .AsNoTracking()
            .AnyAsync(v => !v.IsDeleted 
                        && v.Product != null && v.Product.SellerId == currentSellerId 
                        && v.SkuCode.ToLower() == sku.ToLower(), cancellationToken);

        if (skuExists)
        {
            return new VariantOperationResult<VariantDetailDto>(
                VariantOperationStatus.DuplicateSku,
                ErrorMessage: $"SKU code '{sku}' already exists in your shop.");
        }

        if (request.Price <= 0)
        {
            return new VariantOperationResult<VariantDetailDto>(
                VariantOperationStatus.InvalidOperation,
                ErrorMessage: "Price must be greater than 0.");
        }

        var now = DateTimeOffset.UtcNow;
        var hasActiveDefault = product.Variants.Any(v => !v.IsDeleted && v.IsActive);

        var variant = new Catalog.Domain.Entities.ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            SkuCode = sku,
            Name = request.Name.Trim(),
            Price = request.Price,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "VND" : request.Currency.Trim().ToUpperInvariant(),
            IsActive = !hasActiveDefault,
            HasOrder = false,
            Status = VariantStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = currentSellerId,
            UpdatedBy = currentSellerId,
            IsDeleted = false
        };

        if (request.Attributes != null)
        {
            foreach (var attr in request.Attributes.Where(a => !string.IsNullOrWhiteSpace(a.AttrName)))
            {
                variant.Attributes.Add(new Catalog.Domain.Entities.ProductVariantAttribute
                {
                    Id = Guid.NewGuid(),
                    VariantId = variant.Id,
                    AttrName = attr.AttrName.Trim(),
                    AttrValue = attr.AttrValue.Trim()
                });
            }
        }

        _dbContext.ProductVariants.Add(variant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new VariantDetailDto(
            variant.Id,
            variant.ProductId,
            variant.SkuCode,
            variant.Name,
            variant.Price,
            variant.Currency,
            variant.IsActive,
            variant.HasOrder,
            variant.Status.ToString(),
            variant.StatusReason?.ToString(),
            variant.Attributes.Select(a => new ProductVariantAttributeDto(a.AttrName, a.AttrValue)).ToList()
        );

        return new VariantOperationResult<VariantDetailDto>(VariantOperationStatus.Success, dto);
    }

    // CAT-08: Update Variant
    public async Task<VariantOperationResult<VariantDetailDto>> UpdateVariantAsync(
        Guid variantId,
        Guid currentSellerId,
        UpdateVariantRequest request,
        CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants
            .Include(v => v.Product)
            .Include(v => v.Attributes)
            .FirstOrDefaultAsync(v => v.Id == variantId && !v.IsDeleted, cancellationToken);

        if (variant == null)
        {
            return new VariantOperationResult<VariantDetailDto>(
                VariantOperationStatus.NotFound,
                ErrorMessage: $"Variant with ID '{variantId}' was not found in the system.");
        }

        if (variant.Product == null || variant.Product.SellerId != currentSellerId)
        {
            return new VariantOperationResult<VariantDetailDto>(
                VariantOperationStatus.Forbidden,
                ErrorMessage: "You do not have permission to modify this variant.");
        }

        if (request.Price <= 0)
        {
            return new VariantOperationResult<VariantDetailDto>(
                VariantOperationStatus.InvalidOperation,
                ErrorMessage: "Price must be greater than 0.");
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            variant.Name = request.Name.Trim();
        }

        variant.Price = request.Price;
        if (!string.IsNullOrWhiteSpace(request.Currency))
        {
            variant.Currency = request.Currency.Trim().ToUpperInvariant();
        }

        variant.UpdatedAt = DateTimeOffset.UtcNow;
        variant.UpdatedBy = currentSellerId;

        if (request.Attributes != null)
        {
            _dbContext.ProductVariantAttributes.RemoveRange(variant.Attributes);
            variant.Attributes.Clear();

            foreach (var attr in request.Attributes.Where(a => !string.IsNullOrWhiteSpace(a.AttrName)))
            {
                var newAttr = new Catalog.Domain.Entities.ProductVariantAttribute
                {
                    Id = Guid.NewGuid(),
                    VariantId = variant.Id,
                    AttrName = attr.AttrName.Trim(),
                    AttrValue = attr.AttrValue.Trim()
                };
                variant.Attributes.Add(newAttr);
                _dbContext.ProductVariantAttributes.Add(newAttr);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new VariantDetailDto(
            variant.Id,
            variant.ProductId,
            variant.SkuCode,
            variant.Name,
            variant.Price,
            variant.Currency,
            variant.IsActive,
            variant.HasOrder,
            variant.Status.ToString(),
            variant.StatusReason?.ToString(),
            variant.Attributes.Select(a => new ProductVariantAttributeDto(a.AttrName, a.AttrValue)).ToList()
        );

        return new VariantOperationResult<VariantDetailDto>(VariantOperationStatus.Success, dto);
    }

    // CAT-09: Change Variant Status
    public async Task<VariantOperationResult<VariantDetailDto>> ChangeVariantStatusAsync(
        Guid variantId,
        Guid currentSellerId,
        ChangeVariantStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants
            .Include(v => v.Product)
            .Include(v => v.Attributes)
            .FirstOrDefaultAsync(v => v.Id == variantId && !v.IsDeleted, cancellationToken);

        if (variant == null)
        {
            return new VariantOperationResult<VariantDetailDto>(
                VariantOperationStatus.NotFound,
                ErrorMessage: $"Variant with ID '{variantId}' was not found in the system.");
        }

        if (variant.Product == null || variant.Product.SellerId != currentSellerId)
        {
            return new VariantOperationResult<VariantDetailDto>(
                VariantOperationStatus.Forbidden,
                ErrorMessage: "You do not have permission to modify this variant.");
        }

        var normalizedStatus = (request.Status ?? string.Empty).Replace("_", string.Empty);
        if (!Enum.TryParse<VariantStatus>(normalizedStatus, true, out var newStatus))
        {
            return new VariantOperationResult<VariantDetailDto>(
                VariantOperationStatus.InvalidOperation,
                ErrorMessage: "Status must be ACTIVE or INACTIVE.");
        }

        if (newStatus == VariantStatus.Inactive)
        {
            var normalizedReason = (request.StatusReason ?? string.Empty).Replace("_", string.Empty);
            if (string.IsNullOrWhiteSpace(request.StatusReason) || !Enum.TryParse<VariantInactiveReason>(normalizedReason, true, out var reason))
            {
                return new VariantOperationResult<VariantDetailDto>(
                    VariantOperationStatus.InvalidOperation,
                    ErrorMessage: "StatusReason is required when setting variant to INACTIVE (must be OUT_OF_STOCK, DISCONTINUED, or OTHER).");
            }

            if (variant.Product.Status == ProductStatus.Active)
            {
                var otherActiveVariants = await _dbContext.ProductVariants
                    .AnyAsync(v => v.ProductId == variant.ProductId && v.Id != variant.Id && !v.IsDeleted && v.Status == VariantStatus.Active, cancellationToken);

                if (!otherActiveVariants)
                {
                    return new VariantOperationResult<VariantDetailDto>(
                        VariantOperationStatus.InvalidOperation,
                        ErrorMessage: "Cannot deactivate the last active variant of an active product. Deactivate the product instead.");
                }
            }

            variant.StatusReason = reason;
        }
        else
        {
            variant.StatusReason = null;
        }

        variant.Status = newStatus;
        variant.UpdatedAt = DateTimeOffset.UtcNow;
        variant.UpdatedBy = currentSellerId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new VariantDetailDto(
            variant.Id,
            variant.ProductId,
            variant.SkuCode,
            variant.Name,
            variant.Price,
            variant.Currency,
            variant.IsActive,
            variant.HasOrder,
            variant.Status.ToString(),
            variant.StatusReason?.ToString(),
            variant.Attributes.Select(a => new ProductVariantAttributeDto(a.AttrName, a.AttrValue)).ToList()
        );

        return new VariantOperationResult<VariantDetailDto>(VariantOperationStatus.Success, dto);
    }

    // CAT-10: Set Default Variant
    public async Task<VariantOperationResult<Guid>> SetDefaultVariantAsync(
        Guid productId,
        Guid currentSellerId,
        Guid variantId,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, cancellationToken);

        if (product == null)
        {
            return new VariantOperationResult<Guid>(
                VariantOperationStatus.NotFound,
                ErrorMessage: $"Product with ID '{productId}' was not found in the system.");
        }

        if (product.SellerId != currentSellerId)
        {
            return new VariantOperationResult<Guid>(
                VariantOperationStatus.Forbidden,
                ErrorMessage: "You do not have permission to modify this product.");
        }

        var targetVariant = product.Variants.FirstOrDefault(v => v.Id == variantId && !v.IsDeleted);
        if (targetVariant == null)
        {
            return new VariantOperationResult<Guid>(
                VariantOperationStatus.NotFound,
                ErrorMessage: $"Variant with ID '{variantId}' was not found for this product.");
        }

        if (targetVariant.Status != VariantStatus.Active)
        {
            return new VariantOperationResult<Guid>(
                VariantOperationStatus.InvalidOperation,
                ErrorMessage: "Only an active variant can be set as the default variant.");
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var v in product.Variants.Where(v => !v.IsDeleted))
        {
            v.IsActive = (v.Id == variantId);
            v.UpdatedAt = now;
            v.UpdatedBy = currentSellerId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new VariantOperationResult<Guid>(VariantOperationStatus.Success, variantId);
    }

    // CAT-11: Soft Delete Variant
    public async Task<VariantOperationResult<bool>> SoftDeleteVariantAsync(
        Guid variantId,
        Guid currentSellerId,
        CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == variantId && !v.IsDeleted, cancellationToken);

        if (variant == null)
        {
            return new VariantOperationResult<bool>(
                VariantOperationStatus.NotFound,
                false,
                ErrorMessage: $"Variant with ID '{variantId}' was not found in the system.");
        }

        if (variant.Product == null || variant.Product.SellerId != currentSellerId)
        {
            return new VariantOperationResult<bool>(
                VariantOperationStatus.Forbidden,
                false,
                ErrorMessage: "You do not have permission to delete this variant.");
        }

        if (variant.HasOrder)
        {
            return new VariantOperationResult<bool>(
                VariantOperationStatus.HasOrder,
                false,
                ErrorMessage: "Cannot delete a variant that has existing orders. You can set its status to INACTIVE instead.");
        }

        var remainingCount = await _dbContext.ProductVariants
            .CountAsync(v => v.ProductId == variant.ProductId && !v.IsDeleted, cancellationToken);

        if (remainingCount <= 1)
        {
            return new VariantOperationResult<bool>(
                VariantOperationStatus.LastVariant,
                false,
                ErrorMessage: "Cannot delete the only remaining variant of a product.");
        }

        var now = DateTimeOffset.UtcNow;
        variant.IsDeleted = true;
        variant.DeletedAt = now;
        variant.UpdatedAt = now;
        variant.UpdatedBy = currentSellerId;

        // If deleted variant was default, transfer default to another active variant
        if (variant.IsActive)
        {
            var nextDefault = await _dbContext.ProductVariants
                .FirstOrDefaultAsync(v => v.ProductId == variant.ProductId && v.Id != variant.Id && !v.IsDeleted, cancellationToken);

            if (nextDefault != null)
            {
                nextDefault.IsActive = true;
                nextDefault.UpdatedAt = now;
                nextDefault.UpdatedBy = currentSellerId;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new VariantOperationResult<bool>(VariantOperationStatus.Success, true);
    }
}
