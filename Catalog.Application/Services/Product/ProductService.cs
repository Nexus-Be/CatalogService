namespace Catalog.Application.Services.Product;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Catalog.Application.Common;
using Catalog.Application.Common.Extensions;
using Catalog.Application.Common.Models;
using Catalog.Application.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Enums;
using Catalog.Domain.Exceptions;

public sealed class ProductService : IProductService
{
    private const string DefaultImageUrl = "https://placehold.co/600x600/png?text=Nexus+Product";
    private readonly ICatalogDbContext _dbContext;
    private readonly IFulfillmentClient _fulfillmentClient;
    private readonly IImageStorageService _imageStorageService;

    public ProductService(
        ICatalogDbContext dbContext,
        IFulfillmentClient fulfillmentClient,
        IImageStorageService imageStorageService)
    {
        _dbContext = dbContext;
        _fulfillmentClient = fulfillmentClient;
        _imageStorageService = imageStorageService;
    }

    // -------------------------------------------------------------
    // Product Queries & CRUD
    // -------------------------------------------------------------

    public async Task<ProductDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", id);

        return new ProductDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Brand,
            product.Status.ToString());
    }

    public async Task<PagedData<ProductDto>> ListAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = PaginationConstants.Normalize(page, pageSize);

        var query = _dbContext.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.Status == ProductStatus.Active);

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(product => new ProductDto(
                product.Id,
                product.SellerId,
                product.CategoryId,
                product.Name,
                product.Description,
                product.Brand,
                product.Status.ToString()))
            .ToListAsync(cancellationToken);

        return new PagedData<ProductDto>(products, PaginationMetadata.Create(safePage, safePageSize, totalCount));
    }

    public async Task<PagedData<ProductListItemDto>> SearchAsync(ProductListQuery query, CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = PaginationConstants.Normalize(query.Page, query.PageSize);

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

        var isDesc = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        var sortKey = query.SortBy?.Trim().ToLowerInvariant();

        if (sortKey == "priceasc" || sortKey == "price_asc") { sortKey = "price"; isDesc = false; }
        else if (sortKey == "pricedesc" || sortKey == "price_desc") { sortKey = "price"; isDesc = true; }
        else if (sortKey == "rating_desc") { sortKey = "rating"; isDesc = true; }
        else if (sortKey == "rating_asc") { sortKey = "rating"; isDesc = false; }
        else if (sortKey == "name_asc") { sortKey = "name"; isDesc = false; }
        else if (sortKey == "name_desc") { sortKey = "name"; isDesc = true; }
        else if (sortKey == "createdat_asc" || sortKey == "newest_asc") { sortKey = "createdat"; isDesc = false; }
        else if (sortKey == "createdat_desc" || sortKey == "newest" || sortKey == "newest_desc") { sortKey = "createdat"; isDesc = true; }

        IQueryable<Product> ordered = (sortKey, isDesc) switch
        {
            ("price", false) => products
                .OrderBy(p => p.Variants.Where(v => !v.IsDeleted && v.Status == VariantStatus.Active).Min(v => v.Price))
                .ThenBy(p => p.Id),
            ("price", true) => products
                .OrderByDescending(p => p.Variants.Where(v => !v.IsDeleted && v.Status == VariantStatus.Active).Min(v => v.Price))
                .ThenBy(p => p.Id),
            ("name", false) => products.OrderBy(p => p.Name).ThenBy(p => p.Id),
            ("name", true) => products.OrderByDescending(p => p.Name).ThenBy(p => p.Id),
            ("rating", false) => products
                .OrderBy(p => p.RatingStats != null ? p.RatingStats.AvgRating : 0)
                .ThenBy(p => p.Id),
            ("rating", true) => products
                .OrderByDescending(p => p.RatingStats != null ? p.RatingStats.AvgRating : 0)
                .ThenBy(p => p.Id),
            ("createdat", false) => products.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id),
            _ => products.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
        };

        var rows = await ordered
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
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

        return new PagedData<ProductListItemDto>(items, PaginationMetadata.Create(safePage, safePageSize, totalCount));
    }

    public async Task<ProductDetailDto> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
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
                    i.Url,
                    i.AltText,
                    i.IsActive,
                    i.SortOrder,
                    i.VariantId
                )).ToList(),
                new ProductDetailRatingDto(
                    p.RatingStats != null ? p.RatingStats.AvgRating : 0,
                    p.RatingStats != null ? p.RatingStats.ReviewCount : 0
                )
            ))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", id);

        return product;
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Product name cannot be empty.");
        }

        var trimmedName = request.Name.Trim();
        if (trimmedName.Length > 255)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Product name cannot exceed 255 characters.");
        }

        if (request.Brand != null && request.Brand.Trim().Length > 150)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Brand name cannot exceed 150 characters.");
        }

        if (request.Description != null && request.Description.Trim().Length > 4000)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Description cannot exceed 4000 characters.");
        }

        var category = await _dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && !c.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("CATEGORY_NOT_FOUND", "Category", request.CategoryId);

        var hasChildren = await _dbContext.Categories
            .AsNoTracking()
            .AnyAsync(c => c.ParentId == request.CategoryId && !c.IsDeleted, cancellationToken);

        if (hasChildren)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Products can only be assigned to a leaf category.");
        }

        var now = DateTimeOffset.UtcNow;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            SellerId = request.SellerId,
            CategoryId = request.CategoryId,
            Name = trimmedName,
            Description = request.Description?.Trim(),
            Brand = request.Brand?.Trim(),
            SaleType = request.SaleType,
            Status = ProductStatus.Draft,
            Specifications = request.Specifications,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = request.SellerId,
            UpdatedBy = request.SellerId,
            IsDeleted = false
        };

        _dbContext.Products.Add(product);

        // Discrepancy #3 Hybrid Solution: If caller provides initial variants, create them within the same transaction!
        if (request.Variants != null && request.Variants.Count > 0)
        {
            if (request.Variants.Count > 10)
            {
                throw new BusinessRuleValidationException("MAX_VARIANTS_REACHED", "A product cannot exceed 10 variants.");
            }

            var requestedSkus = request.Variants.Select(v => v.SkuCode.Trim().ToLower()).ToList();
            if (requestedSkus.Distinct().Count() != requestedSkus.Count)
            {
                throw new BusinessRuleValidationException("DUPLICATE_SKU", "Duplicate SKU code within the variants list.", 409);
            }

            var existingSku = await _dbContext.ProductVariants
                .AsNoTracking()
                .Where(v => !v.IsDeleted 
                         && v.Product != null && v.Product.SellerId == request.SellerId 
                         && requestedSkus.Contains(v.SkuCode.ToLower()))
                .Select(v => v.SkuCode)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingSku != null)
            {
                throw new BusinessRuleValidationException("DUPLICATE_SKU", $"SKU code '{existingSku}' already exists in your shop.", 409);
            }

            for (int i = 0; i < request.Variants.Count; i++)
            {
                var v = request.Variants[i];
                if (v.Price <= 0)
                {
                    throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Price must be greater than 0 for all variants.");
                }

                var variant = new ProductVariant
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    SkuCode = v.SkuCode.Trim(),
                    Name = string.IsNullOrWhiteSpace(v.Name) ? product.Name : v.Name.Trim(),
                    Price = v.Price,
                    Currency = string.IsNullOrWhiteSpace(v.Currency) ? "VND" : v.Currency.Trim().ToUpperInvariant(),
                    IsActive = (i == 0),
                    HasOrder = false,
                    Status = VariantStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = request.SellerId,
                    UpdatedBy = request.SellerId,
                    IsDeleted = false
                };

                if (v.Attributes != null)
                {
                    foreach (var attr in v.Attributes.Where(a => !string.IsNullOrWhiteSpace(a.AttrName)))
                    {
                        variant.Attributes.Add(new ProductVariantAttribute
                        {
                            Id = Guid.NewGuid(),
                            VariantId = variant.Id,
                            AttrName = attr.AttrName.Trim(),
                            AttrValue = attr.AttrValue.Trim()
                        });
                    }
                }

                _dbContext.ProductVariants.Add(variant);
            }
        }

        _dbContext.AddAuditLog(
            AuditAction.ProductCreate,
            "Product",
            product.Id.ToString(),
            newValues: new { product.Name, product.SellerId, product.CategoryId, product.SaleType },
            userId: request.SellerId.ToString());

        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await _fulfillmentClient.InitializeInventoryAsync(
                new InitializeInventoryClientRequest(product.Id, null, 0),
                cancellationToken);
        }
        catch
        {
            // Logging or non-fatal fulfillment init failure
        }

        return new ProductDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Brand,
            product.Status.ToString());
    }

    public async Task<ProductDto> CreateAuctionProductAsync(CreateAuctionProductRequest request, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Product name cannot be empty.");
        }

        var trimmedName = request.Name.Trim();
        if (trimmedName.Length > 255)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Product name cannot exceed 255 characters.");
        }

        var category = await _dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && !c.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("CATEGORY_NOT_FOUND", "Category", request.CategoryId);

        var hasChildren = await _dbContext.Categories
            .AsNoTracking()
            .AnyAsync(c => c.ParentId == request.CategoryId && !c.IsDeleted, cancellationToken);

        if (hasChildren)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Products can only be assigned to a leaf category.");
        }

        var now = DateTimeOffset.UtcNow;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            SellerId = currentUserId,
            CategoryId = request.CategoryId,
            Name = trimmedName,
            Description = request.Description?.Trim(),
            Brand = request.Brand?.Trim(),
            SaleType = SaleType.Auction,
            Status = ProductStatus.Draft,
            Specifications = request.Specifications,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = currentUserId,
            UpdatedBy = currentUserId,
            IsDeleted = false
        };

        var skuCode = !string.IsNullOrWhiteSpace(request.SkuCode)
            ? request.SkuCode.Trim()
            : $"AUC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            SkuCode = skuCode,
            Name = product.Name,
            Price = null,
            Currency = "VND",
            IsActive = true,
            HasOrder = false,
            Status = VariantStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = currentUserId,
            UpdatedBy = currentUserId,
            IsDeleted = false
        };

        _dbContext.Products.Add(product);
        _dbContext.ProductVariants.Add(variant);

        _dbContext.AddAuditLog(
            AuditAction.ProductCreate,
            "Product",
            product.Id.ToString(),
            newValues: new { product.Name, product.SellerId, product.CategoryId, product.SaleType },
            userId: currentUserId.ToString());

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProductDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Brand,
            product.Status.ToString());
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", id);

        if (product.SellerId != currentUserId)
        {
            throw new DomainException("PERMISSION_DENIED", "You do not have permission to modify this product.", 403);
        }

        if (string.IsNullOrWhiteSpace(request?.Name))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Product name cannot be empty.");
        }

        var trimmedName = request.Name.Trim();
        if (trimmedName.Length > 255)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Product name cannot exceed 255 characters.");
        }

        if (request.Brand != null && request.Brand.Trim().Length > 150)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Brand name cannot exceed 150 characters.");
        }

        if (request.Description != null && request.Description.Trim().Length > 4000)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Description cannot exceed 4000 characters.");
        }

        if (request.CategoryId.HasValue && request.CategoryId.Value != Guid.Empty && request.CategoryId.Value != product.CategoryId)
        {
            var categoryExists = await _dbContext.Categories
                .AsNoTracking()
                .AnyAsync(c => c.Id == request.CategoryId.Value && !c.IsDeleted, cancellationToken);

            if (!categoryExists)
            {
                throw new EntityNotFoundException("CATEGORY_NOT_FOUND", "Category", request.CategoryId.Value);
            }

            product.CategoryId = request.CategoryId.Value;
        }

        var oldValues = new { product.Name, product.Description, product.Brand, product.CategoryId, product.SaleType, product.Status };

        product.Name = trimmedName;
        product.Description = request.Description?.Trim();
        product.Brand = request.Brand?.Trim();

        if (request.SaleType.HasValue)
        {
            product.SaleType = request.SaleType.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ProductStatus>(request.Status, true, out var parsedStatus))
        {
            product.Status = parsedStatus;
        }

        if (request.Specifications != null)
        {
            product.Specifications = request.Specifications;
        }

        product.UpdatedAt = DateTimeOffset.UtcNow;
        product.UpdatedBy = currentUserId;

        _dbContext.AddAuditLog(
            AuditAction.ProductUpdate,
            "Product",
            product.Id.ToString(),
            oldValues: oldValues,
            newValues: new { product.Name, product.Description, product.Brand, product.CategoryId, product.SaleType, product.Status },
            userId: currentUserId.ToString());

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProductDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Brand,
            product.Status.ToString());
    }

    public async Task<ProductDto> ChangeStatusAsync(Guid id, ChangeProductStatusRequest request, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Status) ||
            !Enum.TryParse<ProductStatus>(request.Status, true, out var newStatus))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Invalid status value. Allowed values are DRAFT, ACTIVE, INACTIVE.");
        }

        var product = await _dbContext.Products
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", id);

        if (product.SellerId != currentUserId)
        {
            throw new DomainException("PERMISSION_DENIED", "You do not have permission to modify this product.", 403);
        }

        if (newStatus == ProductStatus.Active)
        {
            var hasActiveVariant = product.Variants.Any(v => !v.IsDeleted && v.Status == VariantStatus.Active && (product.SaleType == SaleType.Auction || (v.Price.HasValue && v.Price.Value > 0)));
            if (!hasActiveVariant)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot activate product without at least one active variant.");
            }

            var hasActiveImage = product.Images.Any(i => !i.IsDeleted && i.IsActive);
            if (!hasActiveImage)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot activate product without at least one active product image.");
            }

            product.PublishedAt ??= DateTimeOffset.UtcNow;
            product.StatusReason = null;
        }
        else if (newStatus == ProductStatus.Inactive)
        {
            product.StatusReason = ProductInactiveReason.SellerPaused;
            if (!string.IsNullOrWhiteSpace(request.StatusReason) &&
                Enum.TryParse<ProductInactiveReason>(request.StatusReason, true, out var reason))
            {
                product.StatusReason = reason;
            }
        }
        else if (newStatus == ProductStatus.Draft)
        {
            product.StatusReason = null;
        }

        var oldStatus = product.Status;
        product.Status = newStatus;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        product.UpdatedBy = currentUserId;

        _dbContext.AddAuditLog(
            AuditAction.ProductUpdate,
            "Product",
            product.Id.ToString(),
            oldValues: new { Status = oldStatus.ToString() },
            newValues: new { Status = newStatus.ToString(), Reason = product.StatusReason?.ToString() },
            userId: currentUserId.ToString());

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProductDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Brand,
            product.Status.ToString());
    }

    public async Task<bool> SoftDeleteAsync(Guid id, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", id);

        if (product.SellerId != currentUserId)
        {
            throw new DomainException("PERMISSION_DENIED", "You do not have permission to delete this product.", 403);
        }

        var now = DateTimeOffset.UtcNow;
        product.IsDeleted = true;
        product.DeletedAt = now;
        product.UpdatedAt = now;
        product.UpdatedBy = currentUserId;

        foreach (var variant in product.Variants.Where(v => !v.IsDeleted))
        {
            variant.IsDeleted = true;
            variant.DeletedAt = now;
            variant.UpdatedAt = now;
            variant.UpdatedBy = currentUserId;
        }

        _dbContext.AddAuditLog(
            AuditAction.ProductDelete,
            "Product",
            product.Id.ToString(),
            oldValues: new { product.Name },
            userId: currentUserId.ToString());

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // -------------------------------------------------------------
    // Image Management (CAT-12, CAT-13, CAT-14, CAT-40)
    // -------------------------------------------------------------

    public async Task<IReadOnlyList<ProductImageDto>> GetProductImagesAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var productExists = await _dbContext.Products
            .AsNoTracking()
            .AnyAsync(p => p.Id == productId && !p.IsDeleted, cancellationToken);

        if (!productExists)
        {
            throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", productId);
        }

        var images = await _dbContext.ProductImages
            .AsNoTracking()
            .Where(i => i.ProductId == productId && !i.IsDeleted)
            .OrderBy(i => i.SortOrder)
            .Select(i => new ProductImageDto(
                i.Id,
                i.ProductId,
                i.VariantId,
                i.Url.Contains("nexus.local") ? DefaultImageUrl : i.Url,
                i.AltText,
                i.IsActive,
                i.SortOrder))
            .ToListAsync(cancellationToken);

        return images;
    }

    public async Task<ProductImageDto> AddProductImageAsync(
        Guid productId,
        Guid currentUserId,
        Stream fileStream,
        string fileName,
        Guid? variantId = null,
        string? altText = null,
        short sortOrder = 0,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", productId);

        if (product.SellerId != currentUserId)
        {
            throw new DomainException("PERMISSION_DENIED", "You do not have permission to upload images for this product.", 403);
        }

        if (variantId.HasValue && variantId.Value != Guid.Empty)
        {
            var variantExists = await _dbContext.ProductVariants
                .AsNoTracking()
                .AnyAsync(v => v.Id == variantId.Value && v.ProductId == productId && !v.IsDeleted, cancellationToken);

            if (!variantExists)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_INPUT", $"Variant with ID '{variantId.Value}' does not belong to this product.");
            }
        }

        var currentImageCount = await _dbContext.ProductImages
            .CountAsync(i => i.ProductId == productId && !i.IsDeleted, cancellationToken);

        if (currentImageCount >= 5)
        {
            throw new BusinessRuleValidationException("VAL_LIMIT_EXCEEDED", "A product cannot have more than 5 images.");
        }

        var uploadResult = await _imageStorageService.UploadAsync(fileStream, fileName, "products", cancellationToken);

        var isFirstImage = currentImageCount == 0;
        var image = new ProductImage
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            VariantId = variantId,
            Url = uploadResult.Url,
            AltText = altText ?? product.Name,
            IsActive = isFirstImage,
            SortOrder = sortOrder,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsDeleted = false
        };

        _dbContext.ProductImages.Add(image);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProductImageDto(
            image.Id,
            image.ProductId,
            image.VariantId,
            image.Url,
            image.AltText,
            image.IsActive,
            image.SortOrder);
    }

    public async Task<bool> DeleteProductImageAsync(
        Guid productId,
        Guid imageId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return await DeleteImageByIdAsync(imageId, currentUserId, cancellationToken);
    }

    public async Task<bool> DeleteImageByIdAsync(Guid imageId, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var image = await _dbContext.ProductImages
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.Id == imageId && !i.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("IMAGE_NOT_FOUND", "ProductImage", imageId);

        if (image.Product == null || image.Product.SellerId != currentUserId)
        {
            throw new DomainException("PERMISSION_DENIED", "You do not have permission to delete this image.", 403);
        }

        var now = DateTimeOffset.UtcNow;
        image.IsDeleted = true;
        image.DeletedAt = now;
        image.UpdatedAt = now;

        // If deleted image was primary, transfer to next lowest sort order image
        if (image.IsActive)
        {
            var nextImage = await _dbContext.ProductImages
                .Where(i => i.ProductId == image.ProductId && i.Id != imageId && !i.IsDeleted)
                .OrderBy(i => i.SortOrder)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextImage != null)
            {
                nextImage.IsActive = true;
                nextImage.UpdatedAt = now;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ProductImageDto> UpdateImageAsync(
        Guid imageId,
        UpdateProductImageRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var image = await _dbContext.ProductImages
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.Id == imageId && !i.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("IMAGE_NOT_FOUND", "ProductImage", imageId);

        if (image.Product == null || image.Product.SellerId != currentUserId)
        {
            throw new DomainException("PERMISSION_DENIED", "You do not have permission to modify this image.", 403);
        }

        var now = DateTimeOffset.UtcNow;

        if (request.IsActive == true)
        {
            var otherImages = await _dbContext.ProductImages
                .Where(i => i.ProductId == image.ProductId && i.Id != imageId && !i.IsDeleted && i.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var other in otherImages)
            {
                other.IsActive = false;
                other.UpdatedAt = now;
            }

            image.IsActive = true;
        }

        if (request.SortOrder.HasValue)
        {
            image.SortOrder = request.SortOrder.Value;
        }

        if (request.AltText != null)
        {
            image.AltText = request.AltText.Trim();
        }

        image.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProductImageDto(
            image.Id,
            image.ProductId,
            image.VariantId,
            image.Url.Contains("nexus.local") ? DefaultImageUrl : image.Url,
            image.AltText,
            image.IsActive,
            image.SortOrder);
    }

    // -------------------------------------------------------------
    // Variant Management (CAT-07 to CAT-11, CAT-42)
    // -------------------------------------------------------------

    public async Task<VariantDetailDto> GetVariantByIdAsync(Guid variantId, CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants
            .AsNoTracking()
            .Include(v => v.Attributes)
            .FirstOrDefaultAsync(v => v.Id == variantId && !v.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("VARIANT_NOT_FOUND", "ProductVariant", variantId);

        return new VariantDetailDto(
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
    }

    public async Task<VariantDetailDto> AddVariantAsync(
        Guid productId,
        Guid currentUserId,
        CreateVariantRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", productId);

        if (product.SellerId != currentUserId)
        {
            throw new DomainException("PERMISSION_DENIED", "You do not have permission to add variants to this product.", 403);
        }

        if (product.SaleType != SaleType.Regular)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot add variants to an auction product. Auction products only support a single fixed variant.");
        }

        var currentVariantCount = product.Variants.Count(v => !v.IsDeleted);
        if (currentVariantCount >= 10)
        {
            throw new BusinessRuleValidationException("MAX_VARIANTS_REACHED", "A product cannot exceed 10 variants.");
        }

        var sku = request.SkuCode?.Trim();
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "SKU code is required.");
        }

        var skuExists = await _dbContext.ProductVariants
            .AsNoTracking()
            .AnyAsync(v => !v.IsDeleted 
                        && v.Product != null && v.Product.SellerId == currentUserId 
                        && v.SkuCode.ToLower() == sku.ToLower(), cancellationToken);

        if (skuExists)
        {
            throw new BusinessRuleValidationException("DUPLICATE_SKU", $"SKU code '{sku}' already exists in your shop.", 409);
        }

        if (request.Price <= 0)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Price must be greater than 0.");
        }

        var now = DateTimeOffset.UtcNow;
        var hasActiveDefault = product.Variants.Any(v => !v.IsDeleted && v.IsActive);

        var variant = new ProductVariant
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
            CreatedBy = currentUserId,
            UpdatedBy = currentUserId,
            IsDeleted = false
        };

        if (request.Attributes != null)
        {
            foreach (var attr in request.Attributes.Where(a => !string.IsNullOrWhiteSpace(a.AttrName)))
            {
                variant.Attributes.Add(new ProductVariantAttribute
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

        try
        {
            await _fulfillmentClient.InitializeInventoryAsync(
                new InitializeInventoryClientRequest(variant.Id, null, 0),
                cancellationToken);
        }
        catch
        {
            // Non-blocking fulfillment notification
        }

        return new VariantDetailDto(
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
    }

    public async Task<VariantDetailDto> UpdateVariantAsync(
        Guid variantId,
        Guid currentUserId,
        UpdateVariantRequest request,
        CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants
            .Include(v => v.Product)
            .Include(v => v.Attributes)
            .FirstOrDefaultAsync(v => v.Id == variantId && !v.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("VARIANT_NOT_FOUND", "ProductVariant", variantId);

        if (variant.Product == null || variant.Product.SellerId != currentUserId)
        {
            throw new DomainException("PERMISSION_DENIED", "You do not have permission to modify this variant.", 403);
        }

        if (request.Price <= 0)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Price must be greater than 0.");
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
        variant.UpdatedBy = currentUserId;

        if (request.Attributes != null)
        {
            _dbContext.ProductVariantAttributes.RemoveRange(variant.Attributes);
            variant.Attributes.Clear();

            foreach (var attr in request.Attributes.Where(a => !string.IsNullOrWhiteSpace(a.AttrName)))
            {
                var newAttr = new ProductVariantAttribute
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

        return new VariantDetailDto(
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
    }

    public async Task<VariantDetailDto> ChangeVariantStatusAsync(
        Guid variantId,
        Guid currentUserId,
        ChangeVariantStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants
            .Include(v => v.Product)
            .Include(v => v.Attributes)
            .FirstOrDefaultAsync(v => v.Id == variantId && !v.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("VARIANT_NOT_FOUND", "ProductVariant", variantId);

        if (variant.Product == null || variant.Product.SellerId != currentUserId)
        {
            throw new DomainException("PERMISSION_DENIED", "You do not have permission to modify this variant.", 403);
        }

        var normalizedStatus = (request.Status ?? string.Empty).Replace("_", string.Empty);
        if (!Enum.TryParse<VariantStatus>(normalizedStatus, true, out var newStatus))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Status must be ACTIVE or INACTIVE.");
        }

        if (newStatus == VariantStatus.Inactive)
        {
            var normalizedReason = (request.StatusReason ?? string.Empty).Replace("_", string.Empty);
            if (string.IsNullOrWhiteSpace(request.StatusReason) || !Enum.TryParse<VariantInactiveReason>(normalizedReason, true, out var reason))
            {
                throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "StatusReason is required when setting variant to INACTIVE (must be OUT_OF_STOCK, DISCONTINUED, or OTHER).");
            }

            if (variant.Product.Status == ProductStatus.Active)
            {
                var otherActiveVariants = await _dbContext.ProductVariants
                    .AnyAsync(v => v.ProductId == variant.ProductId && v.Id != variant.Id && !v.IsDeleted && v.Status == VariantStatus.Active, cancellationToken);

                if (!otherActiveVariants)
                {
                    throw new BusinessRuleValidationException("LAST_ACTIVE_VARIANT", "Cannot deactivate the last active variant of an active product. Deactivate the product instead.");
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
        variant.UpdatedBy = currentUserId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new VariantDetailDto(
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
    }

    public async Task<Guid> SetDefaultVariantAsync(
        Guid productId,
        Guid currentUserId,
        Guid variantId,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", productId);

        if (product.SellerId != currentUserId)
        {
            throw new DomainException("PERMISSION_DENIED", "You do not have permission to modify this product.", 403);
        }

        var targetVariant = product.Variants.FirstOrDefault(v => v.Id == variantId && !v.IsDeleted)
            ?? throw new EntityNotFoundException("VARIANT_NOT_FOUND", "ProductVariant", variantId);

        if (targetVariant.Status != VariantStatus.Active)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Only an active variant can be set as the default variant.");
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var v in product.Variants.Where(v => !v.IsDeleted))
        {
            v.IsActive = (v.Id == variantId);
            v.UpdatedAt = now;
            v.UpdatedBy = currentUserId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return variantId;
    }

    public async Task<bool> SoftDeleteVariantAsync(
        Guid variantId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == variantId && !v.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("VARIANT_NOT_FOUND", "ProductVariant", variantId);

        if (variant.Product == null || variant.Product.SellerId != currentUserId)
        {
            throw new DomainException("PERMISSION_DENIED", "You do not have permission to delete this variant.", 403);
        }

        if (variant.HasOrder)
        {
            throw new BusinessRuleValidationException("VARIANT_HAS_ORDERS", "Cannot delete a variant that has existing orders. You can set its status to INACTIVE instead.");
        }

        var remainingCount = await _dbContext.ProductVariants
            .CountAsync(v => v.ProductId == variant.ProductId && !v.IsDeleted, cancellationToken);

        if (remainingCount <= 1)
        {
            throw new BusinessRuleValidationException("LAST_VARIANT", "Cannot delete the only remaining variant of a product.");
        }

        var now = DateTimeOffset.UtcNow;
        variant.IsDeleted = true;
        variant.DeletedAt = now;
        variant.UpdatedAt = now;
        variant.UpdatedBy = currentUserId;

        if (variant.IsActive)
        {
            var nextDefault = await _dbContext.ProductVariants
                .FirstOrDefaultAsync(v => v.ProductId == variant.ProductId && v.Id != variant.Id && !v.IsDeleted, cancellationToken);

            if (nextDefault != null)
            {
                nextDefault.IsActive = true;
                nextDefault.UpdatedAt = now;
                nextDefault.UpdatedBy = currentUserId;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // -------------------------------------------------------------
    // Seller APIs (CAT-44, CAT-45)
    // -------------------------------------------------------------

    public async Task<PagedData<SellerProductListItemDto>> SearchSellerProductsAsync(
        Guid sellerId,
        SellerProductSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = PaginationConstants.Normalize(query.Page, query.PageSize);

        var products = _dbContext.Products
            .AsNoTracking()
            .Where(p => p.SellerId == sellerId && !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim().ToLower();
            products = products.Where(p => p.Name.ToLower().Contains(keyword) || (p.Brand != null && p.Brand.ToLower().Contains(keyword)));
        }

        if (query.Status.HasValue)
        {
            products = products.Where(p => p.Status == query.Status.Value);
        }

        if (query.CategoryId.HasValue)
        {
            products = products.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        if (query.SaleType.HasValue)
        {
            products = products.Where(p => p.SaleType == query.SaleType.Value);
        }

        var totalCount = await products.CountAsync(cancellationToken);

        var isDesc = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        var sortKey = query.SortBy?.Trim().ToLowerInvariant();

        if (sortKey == "createdat_asc") { sortKey = "createdat"; isDesc = false; }
        else if (sortKey == "createdat_desc") { sortKey = "createdat"; isDesc = true; }
        else if (sortKey == "name_asc") { sortKey = "name"; isDesc = false; }
        else if (sortKey == "name_desc") { sortKey = "name"; isDesc = true; }
        else if (sortKey == "price_asc") { sortKey = "price"; isDesc = false; }
        else if (sortKey == "price_desc") { sortKey = "price"; isDesc = true; }

        IQueryable<Product> ordered = (sortKey, isDesc) switch
        {
            ("name", false) => products.OrderBy(p => p.Name).ThenBy(p => p.Id),
            ("name", true) => products.OrderByDescending(p => p.Name).ThenBy(p => p.Id),
            ("price", false) => products
                .OrderBy(p => p.Variants.Where(v => !v.IsDeleted).Min(v => v.Price))
                .ThenBy(p => p.Id),
            ("price", true) => products
                .OrderByDescending(p => p.Variants.Where(v => !v.IsDeleted).Min(v => v.Price))
                .ThenBy(p => p.Id),
            ("createdat", false) => products.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id),
            _ => products.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
        };

        var rows = await ordered
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Brand,
                p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : null,
                p.SaleType,
                p.Status,
                p.StatusReason,
                MinPrice = p.Variants.Where(v => !v.IsDeleted).Min(v => v.Price),
                MaxPrice = p.Variants.Where(v => !v.IsDeleted).Max(v => v.Price),
                PrimaryImageUrl = p.Images
                    .Where(i => !i.IsDeleted && i.IsActive)
                    .Select(i => i.Url)
                    .FirstOrDefault(url => !url.Contains("nexus.local")) ?? DefaultImageUrl,
                VariantCount = p.Variants.Count(v => !v.IsDeleted),
                HasOrder = p.Variants.Any(v => !v.IsDeleted && v.HasOrder),
                p.CreatedAt,
                p.PublishedAt
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new SellerProductListItemDto(
            r.Id,
            r.Name,
            r.Brand,
            r.CategoryId,
            r.CategoryName,
            r.SaleType.ToString(),
            r.Status.ToString(),
            r.StatusReason?.ToString(),
            r.MinPrice,
            r.MaxPrice,
            r.PrimaryImageUrl,
            r.VariantCount,
            r.HasOrder,
            r.CreatedAt,
            r.PublishedAt)).ToList();

        return new PagedData<SellerProductListItemDto>(items, PaginationMetadata.Create(safePage, safePageSize, totalCount));
    }

    public async Task<SellerProductDetailDto> GetSellerProductDetailAsync(
        Guid productId,
        Guid sellerId,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Variants)
                .ThenInclude(v => v.Attributes)
            .Include(p => p.Variants)
                .ThenInclude(v => v.Images)
            .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", productId);

        if (product.SellerId != sellerId)
        {
            throw new DomainException("PERMISSION_DENIED", "You do not have permission to view or manage this product.", 403);
        }

        var activeVariants = product.Variants.Where(v => !v.IsDeleted).ToList();
        var hasProductOrder = activeVariants.Any(v => v.HasOrder);

        var productImages = product.Images
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.SortOrder)
            .Select(i => new ProductImageDto(
                i.Id,
                i.ProductId,
                i.VariantId,
                i.Url.Contains("nexus.local") ? DefaultImageUrl : i.Url,
                i.AltText,
                i.IsActive,
                i.SortOrder))
            .ToList();

        var variantDtos = activeVariants
            .Select(v =>
            {
                var vImages = v.Images
                    .Where(i => !i.IsDeleted)
                    .OrderBy(i => i.SortOrder)
                    .Select(i => new ProductImageDto(
                        i.Id,
                        i.ProductId,
                        i.VariantId,
                        i.Url.Contains("nexus.local") ? DefaultImageUrl : i.Url,
                        i.AltText,
                        i.IsActive,
                        i.SortOrder))
                    .ToList();

                var attrs = v.Attributes
                    .Select(a => new ProductVariantAttributeDto(a.AttrName, a.AttrValue))
                    .ToList();

                return new SellerProductVariantDto(
                    v.Id,
                    v.SkuCode,
                    v.Name,
                    v.Price,
                    v.Currency,
                    v.Status.ToString(),
                    v.StatusReason?.ToString(),
                    v.IsActive,
                    v.HasOrder,
                    attrs,
                    vImages);
            })
            .ToList();

        return new SellerProductDetailDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Category?.Name,
            product.Name,
            product.Description,
            product.Brand,
            product.Specifications,
            product.SaleType.ToString(),
            product.Status.ToString(),
            product.StatusReason?.ToString(),
            hasProductOrder,
            product.CreatedAt,
            product.UpdatedAt,
            product.PublishedAt,
            productImages,
            variantDtos);
    }

    // -------------------------------------------------------------
    // Internal Batch APIs (CAT-24, CAT-39)
    // -------------------------------------------------------------

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
}
