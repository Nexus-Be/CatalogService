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
    private readonly IUserClient _userClient;
    private readonly INotificationClient _notificationClient;

    public ProductService(
        ICatalogDbContext dbContext,
        IFulfillmentClient fulfillmentClient,
        IImageStorageService imageStorageService,
        IUserClient userClient,
        INotificationClient notificationClient)
    {
        _dbContext = dbContext;
        _fulfillmentClient = fulfillmentClient;
        _imageStorageService = imageStorageService;
        _userClient = userClient;
        _notificationClient = notificationClient;
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
            product.Status.ToString(),
            product.WeightGrams);
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
                product.Status.ToString(),
                product.WeightGrams))
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
                p.SellerId,
                p.CategoryId,
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
            r.SellerId,
            r.CategoryId,
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
        var row = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.Id == id && !p.IsDeleted && p.Status == ProductStatus.Active)
            .Select(p => new
            {
                p.Id,
                p.SellerId,
                p.Name,
                p.Description,
                p.Brand,
                SaleType = p.SaleType.ToString(),
                Status = p.Status.ToString(),
                p.Specifications,
                p.WeightGrams,
                p.PublishedAt,
                Category = p.Category == null ? null : new ProductDetailCategoryDto(
                    p.Category.Id,
                    p.Category.ParentId,
                    p.Category.Name,
                    p.Category.Level),
                Variants = p.Variants.Where(v => !v.IsDeleted).Select(v => new ProductDetailVariantDto(
                    v.Id,
                    v.SkuCode,
                    v.Name,
                    v.Price,
                    v.Status.ToString(),
                    v.IsActive,
                    v.Attributes.Select(a => new ProductVariantAttributeDto(a.AttrName, a.AttrValue)).ToList(),
                    p.Images.Where(i => !i.IsDeleted && i.VariantId == v.Id && i.IsActive).OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()
                        ?? p.Images.Where(i => !i.IsDeleted && i.IsActive).OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()
                        ?? DefaultImageUrl,
                    p.Images.Where(i => !i.IsDeleted && i.VariantId == v.Id).OrderBy(i => i.SortOrder).Select(i => new ProductImageDto(i.Id, i.ProductId, i.VariantId, i.Url, i.AltText, i.IsActive, i.SortOrder)).ToList(),
                    v.WeightGrams ?? p.WeightGrams
                )).ToList(),
                Images = p.Images.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder).Select(i => new ProductDetailImageDto(
                    i.Id,
                    i.Url,
                    i.AltText,
                    i.IsActive,
                    i.SortOrder,
                    i.VariantId
                )).ToList(),
                Rating = new ProductDetailRatingDto(
                    p.RatingStats != null ? p.RatingStats.AvgRating : 0,
                    p.RatingStats != null ? p.RatingStats.ReviewCount : 0
                )
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", id);

        return new ProductDetailDto(
            row.Id,
            row.SellerId,
            row.Name,
            row.Description,
            row.Brand,
            row.SaleType,
            row.Status,
            ProductSpecificationHelper.Deserialize(row.Specifications),
            row.PublishedAt,
            row.Category,
            row.Variants,
            row.Images,
            row.Rating,
            row.WeightGrams);
    }

    private async Task ValidateSellerPermissionAsync(Guid sellerId, CancellationToken cancellationToken)
    {
        var permission = await _userClient.CheckPermissionAsync(sellerId, "sell", cancellationToken);
        if (permission != null && !permission.IsAllowed)
        {
            var reason = permission.Reason?.ToLowerInvariant() ?? string.Empty;
            if (reason.Contains("suspend"))
            {
                throw new BusinessRuleValidationException(
                    "SELLER_SUSPENDED",
                    "Seller profile is temporarily suspended.",
                    403);
            }
            if (reason.Contains("profile") || reason.Contains("no seller") || reason.Contains("not approved"))
            {
                throw new BusinessRuleValidationException(
                    "NO_SELLER_PROFILE",
                    "User does not have an approved seller profile.",
                    403);
            }

            throw new BusinessRuleValidationException(
                "SELLER_RESTRICTED",
                "Seller account has an active restriction prohibiting selling actions.",
                403);
        }
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Product name cannot be empty.");
        }

        if (request.SellerId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Seller ID cannot be empty.");
        }

        await ValidateSellerPermissionAsync(request.SellerId, cancellationToken);

        if (request.WeightGrams.HasValue && request.WeightGrams.Value <= 0)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "WeightGrams must be greater than 0.");
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

        if (request.WeightGrams.HasValue && request.WeightGrams.Value <= 0)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "WeightGrams must be greater than 0.");
        }

        var category = await _dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && !c.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("CATEGORY_NOT_FOUND", "Category", request.CategoryId);

        if (category.IsDisabled)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot assign product to a disabled category.");
        }

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
            Specifications = ProductSpecificationHelper.ValidateAndSerialize(request.Specifications),
            WeightGrams = request.WeightGrams,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = request.SellerId,
            UpdatedBy = request.SellerId,
            IsDeleted = false
        };

        _dbContext.Products.Add(product);

        var isAuction = request.SaleType == SaleType.Auction;

        if (isAuction)
        {
            // For auction products: exactly one auction variant with Price = null
            string aucSku;
            string aucName = product.Name;
            List<CreateVariantAttributeRequest>? aucAttributes = null;

            if (request.Variants != null && request.Variants.Count > 0)
            {
                var firstVariant = request.Variants[0];
                aucSku = !string.IsNullOrWhiteSpace(firstVariant.SkuCode)
                    ? firstVariant.SkuCode.Trim()
                    : $"AUC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
                if (!string.IsNullOrWhiteSpace(firstVariant.Name)) aucName = firstVariant.Name.Trim();
                aucAttributes = firstVariant.Attributes;
            }
            else
            {
                aucSku = $"AUC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
            }

            var aucVariant = new ProductVariant
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                SkuCode = aucSku,
                Name = aucName,
                Price = null,
                Currency = "VND",
                IsActive = true,
                HasOrder = false,
                Status = VariantStatus.Active,
                WeightGrams = request.WeightGrams,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = request.SellerId,
                UpdatedBy = request.SellerId,
                IsDeleted = false
            };

            if (aucAttributes != null)
            {
                foreach (var attr in aucAttributes.Where(a => !string.IsNullOrWhiteSpace(a.AttrName)))
                {
                    aucVariant.Attributes.Add(new ProductVariantAttribute
                    {
                        Id = Guid.NewGuid(),
                        VariantId = aucVariant.Id,
                        AttrName = attr.AttrName.Trim(),
                        AttrValue = attr.AttrValue.Trim()
                    });
                }
            }

            _dbContext.ProductVariants.Add(aucVariant);
        }
        else if (request.Variants != null && request.Variants.Count > 0)
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
                if (!v.Price.HasValue || v.Price.Value <= 0)
                {
                    throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Price must be greater than 0 for all regular product variants.");
                }

                if (v.WeightGrams.HasValue && v.WeightGrams.Value <= 0)
                {
                    throw new BusinessRuleValidationException("VAL_INVALID_INPUT", $"WeightGrams for variant '{v.SkuCode}' must be greater than 0.");
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
                    WeightGrams = v.WeightGrams ?? request.WeightGrams,
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

        return new ProductDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Brand,
            product.Status.ToString(),
            product.WeightGrams);
    }

    public async Task<ProductDto> CreateAuctionProductAsync(CreateAuctionProductRequest request, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Product name cannot be empty.");
        }

        if (currentUserId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Seller ID cannot be empty.");
        }

        await ValidateSellerPermissionAsync(currentUserId, cancellationToken);

        if (request.SellerId.HasValue && request.SellerId.Value != Guid.Empty && request.SellerId.Value != currentUserId)
        {
            throw new BusinessRuleValidationException("FORBIDDEN", "You cannot create products on behalf of another seller.", 403);
        }

        if (request.WeightGrams.HasValue && request.WeightGrams.Value <= 0)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "WeightGrams must be greater than 0.");
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

        if (category.IsDisabled)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot assign product to a disabled category.");
        }

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
            Specifications = ProductSpecificationHelper.ValidateAndSerialize(request.Specifications),
            WeightGrams = request.WeightGrams,
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
            WeightGrams = request.WeightGrams,
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
            product.Status.ToString(),
            product.WeightGrams);
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

        if (product.Status == ProductStatus.Banned)
        {
            throw new BusinessRuleValidationException("PRODUCT_BANNED", "Cannot modify a banned product.");
        }

        if (product.Status == ProductStatus.InAuction)
        {
            throw new BusinessRuleValidationException("PRODUCT_IN_AUCTION", "Product is currently in an active auction and cannot be modified.");
        }

        if (product.Status == ProductStatus.Auctioned)
        {
            throw new BusinessRuleValidationException("PRODUCT_ALREADY_AUCTIONED", "Auctioned products cannot be modified.");
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
            var category = await _dbContext.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId.Value && !c.IsDeleted, cancellationToken)
                ?? throw new EntityNotFoundException("CATEGORY_NOT_FOUND", "Category", request.CategoryId.Value);

            if (category.IsDisabled)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot assign product to a disabled category.");
            }

            var hasChildren = await _dbContext.Categories
                .AsNoTracking()
                .AnyAsync(c => c.ParentId == request.CategoryId.Value && !c.IsDeleted, cancellationToken);

            if (hasChildren)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Products can only be assigned to a leaf category.");
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
            if (parsedStatus != product.Status)
            {
                throw new BusinessRuleValidationException(
                    "VAL_INVALID_INPUT",
                    "Product status cannot be changed via this general update endpoint. Please use 'PATCH /api/v1/products/{id}/status' to transition product status.");
            }
        }

        if (request.Specifications != null)
        {
            product.Specifications = ProductSpecificationHelper.ValidateAndSerialize(request.Specifications);
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
            product.Status.ToString(),
            product.WeightGrams);
    }

    public async Task<ProductDto> ChangeStatusAsync(
        Guid id,
        ChangeProductStatusRequest request,
        Guid currentUserId,
        bool isAdminOrStaff = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Status) ||
            !Enum.TryParse<ProductStatus>(request.Status, true, out var newStatus))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Invalid status value. Allowed values are DRAFT, ACTIVE, INACTIVE, AUCTIONED, PENDING_APPROVAL.");
        }

        var product = await _dbContext.Products
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", id);

        if (!isAdminOrStaff && product.SellerId != currentUserId)
        {
            throw new DomainException("PERMISSION_DENIED", "You do not have permission to modify this product.", 403);
        }

        if (product.Status == ProductStatus.Banned)
        {
            throw new BusinessRuleValidationException("PRODUCT_BANNED", "This product has been permanently banned and cannot change status.");
        }

        if (product.Status == ProductStatus.InAuction)
        {
            throw new BusinessRuleValidationException("PRODUCT_IN_AUCTION", "Product is currently in an active auction and cannot change status.");
        }

        if (product.Status == ProductStatus.Auctioned)
        {
            throw new BusinessRuleValidationException("PRODUCT_ALREADY_AUCTIONED", "Auctioned products are permanently locked and cannot change status.");
        }

        if (newStatus == ProductStatus.Active)
        {
            if (!isAdminOrStaff)
            {
                throw new BusinessRuleValidationException(
                    "APPROVAL_REQUIRED",
                    "Sellers cannot directly activate products. Products must be approved by an administrator or staff member before going live. Please set status to PENDING_APPROVAL.",
                    403);
            }

            if (product.StatusReason == ProductInactiveReason.AdminLocked)
            {
                throw new BusinessRuleValidationException("PRODUCT_LOCKED_BY_ADMIN", "This product was suspended by administrator and cannot be reactivated by seller. Please contact support.");
            }
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

            if (product.SaleType == SaleType.Auction)
            {
                if (!product.WeightGrams.HasValue || product.WeightGrams.Value <= 0)
                {
                    throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot activate auction product without valid shipping weight. Product must declare WeightGrams greater than 0.");
                }
            }
            else
            {
                var productHasWeight = product.WeightGrams.HasValue && product.WeightGrams.Value > 0;
                if (!productHasWeight)
                {
                    var variantsMissingWeight = product.Variants
                        .Where(v => !v.IsDeleted && v.Status == VariantStatus.Active)
                        .Any(v => !v.WeightGrams.HasValue || v.WeightGrams.Value <= 0);

                    if (variantsMissingWeight)
                    {
                        throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot activate product without valid shipping weight. Either declare WeightGrams on product or provide WeightGrams greater than 0 for all active variants.");
                    }
                }
            }

            product.PublishedAt ??= DateTimeOffset.UtcNow;
            product.StatusReason = null;
        }
        else if (newStatus == ProductStatus.PendingApproval)
        {
            if (product.StatusReason == ProductInactiveReason.AdminLocked)
            {
                throw new BusinessRuleValidationException("PRODUCT_LOCKED_BY_ADMIN", "This product was suspended by administrator and cannot be submitted for approval. Please contact support.");
            }

            // Ensure non-deleted variants with valid price are active for approval review
            foreach (var v in product.Variants.Where(v => !v.IsDeleted && (product.SaleType == SaleType.Auction || (v.Price.HasValue && v.Price.Value > 0))))
            {
                v.Status = VariantStatus.Active;
            }

            var hasActiveVariant = product.Variants.Any(v => !v.IsDeleted && v.Status == VariantStatus.Active && (product.SaleType == SaleType.Auction || (v.Price.HasValue && v.Price.Value > 0)));
            if (!hasActiveVariant)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot submit product for approval without at least one active variant with a valid price.");
            }

            var hasActiveImage = product.Images.Any(i => !i.IsDeleted && i.IsActive);
            if (!hasActiveImage)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot submit product for approval without at least one active product image.");
            }

            if (product.SaleType == SaleType.Auction)
            {
                if (!product.WeightGrams.HasValue || product.WeightGrams.Value <= 0)
                {
                    throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot submit auction product for approval without valid shipping weight. Product must declare WeightGrams greater than 0.");
                }
            }
            else
            {
                var productHasWeight = product.WeightGrams.HasValue && product.WeightGrams.Value > 0;
                if (!productHasWeight)
                {
                    var variantsMissingWeight = product.Variants
                        .Where(v => !v.IsDeleted && v.Status == VariantStatus.Active)
                        .Any(v => !v.WeightGrams.HasValue || v.WeightGrams.Value <= 0);

                    if (variantsMissingWeight)
                    {
                        throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot submit product for approval without valid shipping weight. Either declare WeightGrams on product or provide WeightGrams greater than 0 for all active variants.");
                    }
                }
            }

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
        else if (newStatus == ProductStatus.Auctioned)
        {
            if (product.SaleType != SaleType.Auction)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Only auction products can transition to Auctioned status.");
            }
            if (product.Status != ProductStatus.Active)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Only active auction products can transition to Auctioned status.");
            }

            product.StatusReason = null;
            foreach (var v in product.Variants.Where(v => !v.IsDeleted))
            {
                v.Status = VariantStatus.Inactive;
            }
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
            product.Status.ToString(),
            product.WeightGrams);
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

        if (product.Status == ProductStatus.Banned)
        {
            throw new BusinessRuleValidationException("PRODUCT_BANNED", "Banned products cannot be deleted.");
        }

        if (product.Status == ProductStatus.InAuction)
        {
            throw new BusinessRuleValidationException("PRODUCT_IN_AUCTION", "Product is currently in an active auction and cannot be deleted.");
        }

        if (product.Status == ProductStatus.Auctioned)
        {
            throw new BusinessRuleValidationException("PRODUCT_ALREADY_AUCTIONED", "Auctioned products cannot be deleted.");
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

        var totalProductImageCount = await _dbContext.ProductImages
            .CountAsync(i => i.ProductId == productId && !i.IsDeleted, cancellationToken);

        if (totalProductImageCount >= 15)
        {
            throw new BusinessRuleValidationException("VAL_LIMIT_EXCEEDED", "Total images for a product and its variants cannot exceed 15.");
        }

        bool isFirstImage;
        if (variantId.HasValue && variantId.Value != Guid.Empty)
        {
            var variantImageCount = await _dbContext.ProductImages
                .CountAsync(i => i.VariantId == variantId.Value && !i.IsDeleted, cancellationToken);

            if (variantImageCount >= 3)
            {
                throw new BusinessRuleValidationException("VAL_LIMIT_EXCEEDED", "A variant cannot have more than 3 images.");
            }

            isFirstImage = (variantImageCount == 0);
        }
        else
        {
            var generalImageCount = await _dbContext.ProductImages
                .CountAsync(i => i.ProductId == productId && i.VariantId == null && !i.IsDeleted, cancellationToken);

            if (generalImageCount >= 5)
            {
                throw new BusinessRuleValidationException("VAL_LIMIT_EXCEEDED", "A product cannot have more than 5 general images.");
            }

            isFirstImage = (generalImageCount == 0);
        }

        var uploadFolder = variantId.HasValue ? "products/variants" : "products";
        var uploadResult = await _imageStorageService.UploadAsync(fileStream, fileName, uploadFolder, cancellationToken);
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
        var image = await _dbContext.ProductImages
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == imageId && !i.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("IMAGE_NOT_FOUND", "ProductImage", imageId);

        if (image.ProductId != productId)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "The specified image does not belong to this product.");
        }

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

        if (image.Product.Status == ProductStatus.InAuction || image.Product.Status == ProductStatus.Auctioned)
        {
            throw new BusinessRuleValidationException("PRODUCT_IN_AUCTION", "Cannot delete images of a product that is currently in auction or already auctioned.");
        }

        var remainingImagesCount = await _dbContext.ProductImages
            .CountAsync(i => i.ProductId == image.ProductId && i.Id != imageId && !i.IsDeleted, cancellationToken);

        if (image.Product.Status == ProductStatus.Active && remainingImagesCount == 0)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot delete the only image of an active product. Deactivate the product first or upload another image.");
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
            .Include(v => v.Product)
                .ThenInclude(p => p!.Images)
            .Include(v => v.Images)
            .Include(v => v.Attributes)
            .FirstOrDefaultAsync(v => v.Id == variantId && !v.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("VARIANT_NOT_FOUND", "ProductVariant", variantId);

        var isPurchasable = !variant.IsDeleted &&
            variant.Status == VariantStatus.Active &&
            variant.Product != null &&
            !variant.Product.IsDeleted &&
            variant.Product.Status == ProductStatus.Active &&
            (variant.Product.SaleType != SaleType.Regular || (variant.Price.HasValue && variant.Price.Value > 0));

        var vImages = variant.Images
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

        var primaryImageUrl = vImages.FirstOrDefault(i => i.IsActive)?.Url
            ?? variant.Product?.Images.Where(i => !i.IsDeleted && i.IsActive).OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()
            ?? DefaultImageUrl;

        return new VariantDetailDto(
            variant.Id,
            variant.ProductId,
            variant.Product?.SellerId ?? Guid.Empty,
            variant.Product?.Name ?? string.Empty,
            variant.SkuCode,
            variant.Name,
            variant.Price,
            variant.Currency,
            variant.IsActive,
            variant.HasOrder,
            variant.Status.ToString(),
            variant.StatusReason?.ToString(),
            isPurchasable,
            variant.Attributes.Select(a => new ProductVariantAttributeDto(a.AttrName, a.AttrValue)).ToList(),
            primaryImageUrl,
            vImages,
            variant.WeightGrams ?? variant.Product?.WeightGrams
        );
    }

    public async Task<IReadOnlyList<ProductImageDto>> GetVariantImagesAsync(Guid variantId, CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product)
                .ThenInclude(p => p!.Images)
            .Include(v => v.Images)
            .FirstOrDefaultAsync(v => v.Id == variantId && !v.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("VARIANT_NOT_FOUND", "ProductVariant", variantId);

        var variantImages = variant.Images
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

        if (variantImages.Count == 0 && variant.Product != null)
        {
            var fallback = variant.Product.Images
                .Where(i => !i.IsDeleted && i.IsActive)
                .OrderBy(i => i.SortOrder)
                .Select(i => new ProductImageDto(
                    i.Id,
                    i.ProductId,
                    null,
                    i.Url.Contains("nexus.local") ? DefaultImageUrl : i.Url,
                    i.AltText,
                    i.IsActive,
                    i.SortOrder))
                .ToList();

            return fallback;
        }

        return variantImages;
    }

    public async Task<ProductImageDto> AddVariantImageAsync(
        Guid variantId,
        Guid currentUserId,
        Stream fileStream,
        string fileName,
        string? altText = null,
        short sortOrder = 0,
        CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == variantId && !v.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("VARIANT_NOT_FOUND", "ProductVariant", variantId);

        if (variant.Product == null || variant.Product.SellerId != currentUserId)
        {
            throw new DomainException("PERMISSION_DENIED", "You do not have permission to upload images for this variant.", 403);
        }

        var totalProductImageCount = await _dbContext.ProductImages
            .CountAsync(i => i.ProductId == variant.ProductId && !i.IsDeleted, cancellationToken);

        if (totalProductImageCount >= 15)
        {
            throw new BusinessRuleValidationException("VAL_LIMIT_EXCEEDED", "Total images for a product and its variants cannot exceed 15.");
        }

        var variantImageCount = await _dbContext.ProductImages
            .CountAsync(i => i.VariantId == variantId && !i.IsDeleted, cancellationToken);

        if (variantImageCount >= 3)
        {
            throw new BusinessRuleValidationException("VAL_LIMIT_EXCEEDED", "A variant cannot have more than 3 images.");
        }

        var uploadResult = await _imageStorageService.UploadAsync(fileStream, fileName, "products/variants", cancellationToken);

        var isFirstImage = (variantImageCount == 0);
        var image = new ProductImage
        {
            Id = Guid.NewGuid(),
            ProductId = variant.ProductId,
            VariantId = variantId,
            Url = uploadResult.Url,
            AltText = altText ?? variant.Name,
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

        if (product.Status == ProductStatus.Auctioned)
        {
            throw new BusinessRuleValidationException("PRODUCT_ALREADY_AUCTIONED", "Cannot add variants to an auctioned product.");
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

        if (!request.Price.HasValue || request.Price.Value <= 0)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Price must be greater than 0.");
        }

        if (request.WeightGrams.HasValue && request.WeightGrams.Value <= 0)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "WeightGrams must be greater than 0.");
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
            WeightGrams = request.WeightGrams ?? product.WeightGrams,
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

        using (var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            try
            {
                _dbContext.ProductVariants.Add(variant);
                await _dbContext.SaveChangesAsync(cancellationToken);

                // If product is already Active, register the new SKU in Fulfillment warehouse with initialQuantity = 0
                if (product.SaleType == SaleType.Regular && product.Status == ProductStatus.Active)
                {
                    var invResult = await _fulfillmentClient.InitializeInventoryForSkuAsync(
                        variant.Id,
                        product.SellerId,
                        initialQuantity: 0,
                        cancellationToken);

                    if (invResult == null)
                    {
                        throw new DomainException(
                            "FULFILLMENT_UNAVAILABLE",
                            $"Cannot initialize inventory for SKU '{variant.SkuCode}'. Fulfillment service is unavailable or returned an error.",
                            502);
                    }
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        var isPurchasable = !variant.IsDeleted &&
            variant.Status == VariantStatus.Active &&
            product.Status == ProductStatus.Active &&
            !product.IsDeleted &&
            (product.SaleType != SaleType.Regular || (variant.Price.HasValue && variant.Price.Value > 0));

        return new VariantDetailDto(
            variant.Id,
            variant.ProductId,
            product.SellerId,
            product.Name,
            variant.SkuCode,
            variant.Name,
            variant.Price,
            variant.Currency,
            variant.IsActive,
            variant.HasOrder,
            variant.Status.ToString(),
            variant.StatusReason?.ToString(),
            isPurchasable,
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

        if (request.WeightGrams.HasValue)
        {
            if (request.WeightGrams.Value <= 0)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Variant WeightGrams must be greater than 0.");
            }
            variant.WeightGrams = request.WeightGrams.Value;
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

        var isPurchasable = !variant.IsDeleted &&
            variant.Status == VariantStatus.Active &&
            variant.Product != null &&
            !variant.Product.IsDeleted &&
            variant.Product.Status == ProductStatus.Active &&
            (variant.Product.SaleType != SaleType.Regular || (variant.Price.HasValue && variant.Price.Value > 0));

        return new VariantDetailDto(
            variant.Id,
            variant.ProductId,
            variant.Product?.SellerId ?? Guid.Empty,
            variant.Product?.Name ?? string.Empty,
            variant.SkuCode,
            variant.Name,
            variant.Price,
            variant.Currency,
            variant.IsActive,
            variant.HasOrder,
            variant.Status.ToString(),
            variant.StatusReason?.ToString(),
            isPurchasable,
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

        var isPurchasable = !variant.IsDeleted &&
            variant.Status == VariantStatus.Active &&
            variant.Product != null &&
            !variant.Product.IsDeleted &&
            variant.Product.Status == ProductStatus.Active &&
            (variant.Product.SaleType != SaleType.Regular || (variant.Price.HasValue && variant.Price.Value > 0));

        return new VariantDetailDto(
            variant.Id,
            variant.ProductId,
            variant.Product?.SellerId ?? Guid.Empty,
            variant.Product?.Name ?? string.Empty,
            variant.SkuCode,
            variant.Name,
            variant.Price,
            variant.Currency,
            variant.IsActive,
            variant.HasOrder,
            variant.Status.ToString(),
            variant.StatusReason?.ToString(),
            isPurchasable,
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
                    vImages,
                    v.WeightGrams);
            })
            .ToList();

        string? rejectionReason = null;
        if (product.StatusReason == ProductInactiveReason.Rejected)
        {
            var lastAudit = await _dbContext.AuditLogs
                .AsNoTracking()
                .Where(a => a.EntityName == "Product" && a.EntityId == product.Id.ToString())
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(lastAudit?.NewValues))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(lastAudit.NewValues);
                    if (doc.RootElement.TryGetProperty("Note", out var noteProp) && !string.IsNullOrWhiteSpace(noteProp.GetString()))
                    {
                        rejectionReason = noteProp.GetString();
                    }
                }
                catch
                {
                }
            }
        }

        return new SellerProductDetailDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Category?.Name,
            product.Name,
            product.Description,
            product.Brand,
            ProductSpecificationHelper.Deserialize(product.Specifications),
            product.SaleType.ToString(),
            product.Status.ToString(),
            product.StatusReason?.ToString(),
            hasProductOrder,
            product.CreatedAt,
            product.UpdatedAt,
            product.PublishedAt,
            productImages,
            variantDtos,
            product.WeightGrams,
            rejectionReason);
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

            var effectiveWeight = v.WeightGrams ?? v.Product?.WeightGrams;
            var isPurchasable = !v.IsDeleted &&
                v.Status == VariantStatus.Active &&
                v.Product != null &&
                !v.Product.IsDeleted &&
                v.Product.Status == ProductStatus.Active &&
                (v.Product.SaleType != SaleType.Regular || (v.Price.HasValue && v.Price.Value > 0)) &&
                (effectiveWeight.HasValue && effectiveWeight.Value > 0);

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
                Attributes: attrs,
                IsPurchasable: isPurchasable,
                WeightGrams: effectiveWeight
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
            else if (v.Status != VariantStatus.Active)
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

            var effectiveWeight = v.WeightGrams ?? prod?.WeightGrams;
            if (purpose is "ORDER" or "CHECKOUT" && (!effectiveWeight.HasValue || effectiveWeight.Value <= 0))
            {
                isPurchasable = false;
                reason = "MISSING_SHIPPING_WEIGHT";
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

    public async Task<ValidateVariantSwitchResponse> ValidateVariantSwitchAsync(
        ValidateVariantSwitchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || request.CurrentSkuId == Guid.Empty || request.NewSkuId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Current SKU ID and new SKU ID must be provided.");
        }

        var requestedIds = new List<Guid> { request.CurrentSkuId, request.NewSkuId }.Distinct().ToList();

        var variants = await _dbContext.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product)
            .Where(v => requestedIds.Contains(v.Id))
            .ToListAsync(cancellationToken);

        var currentVariant = variants.FirstOrDefault(v => v.Id == request.CurrentSkuId);
        if (currentVariant == null || currentVariant.IsDeleted)
        {
            throw new EntityNotFoundException("CATALOG_SKU_NOT_FOUND", "ProductVariant", request.CurrentSkuId);
        }

        var newVariant = variants.FirstOrDefault(v => v.Id == request.NewSkuId);
        if (newVariant == null || newVariant.IsDeleted)
        {
            throw new EntityNotFoundException("CATALOG_SKU_NOT_FOUND", "ProductVariant", request.NewSkuId);
        }

        if (currentVariant.ProductId != newVariant.ProductId)
        {
            throw new BusinessRuleValidationException("CATALOG_VARIANT_MISMATCH", "New SKU does not belong to the same parent product.", 400);
        }

        var product = newVariant.Product;
        var isPurchasable = true;

        if (product == null || product.IsDeleted || product.Status != ProductStatus.Active)
        {
            isPurchasable = false;
        }
        else if (newVariant.Status != VariantStatus.Active)
        {
            isPurchasable = false;
        }
        else if (product.SaleType == SaleType.Regular && (!newVariant.Price.HasValue || newVariant.Price.Value <= 0))
        {
            isPurchasable = false;
        }

        var unitPrice = newVariant.Price ?? 0m;
        var sellerId = product?.SellerId ?? Guid.Empty;
        var effectiveWeight = newVariant.WeightGrams ?? product?.WeightGrams;

        if (!effectiveWeight.HasValue || effectiveWeight.Value <= 0)
        {
            isPurchasable = false;
        }

        return new ValidateVariantSwitchResponse(
            IsValid: true,
            ProductId: currentVariant.ProductId,
            SellerId: sellerId,
            NewSkuId: newVariant.Id,
            NewSkuName: newVariant.Name,
            NewUnitPrice: unitPrice,
            IsPurchasable: isPurchasable,
            WeightGrams: effectiveWeight
        );
    }

    // -------------------------------------------------------------
    // Dedicated Auction APIs (CAT-46, CAT-47, CAT-48, CAT-49)
    // -------------------------------------------------------------

    public async Task<PagedData<AuctionProductListItemDto>> SearchAuctionsAsync(
        AuctionListQuery query,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = PaginationConstants.Normalize(query.Page, query.PageSize);

        var products = _dbContext.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.SaleType == SaleType.Auction);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!string.Equals(query.Status, "all", StringComparison.OrdinalIgnoreCase) &&
                Enum.TryParse<ProductStatus>(query.Status, true, out var filterStatus))
            {
                products = products.Where(p => p.Status == filterStatus);
            }
        }
        else
        {
            // Default: Active or InAuction
            products = products.Where(p => p.Status == ProductStatus.Active || p.Status == ProductStatus.InAuction);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim().ToLower();
            products = products.Where(p => p.Name.ToLower().Contains(keyword) || (p.Brand != null && p.Brand.ToLower().Contains(keyword)));
        }

        if (query.CategoryId.HasValue)
        {
            products = products.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        if (query.SellerId.HasValue)
        {
            products = products.Where(p => p.SellerId == query.SellerId.Value);
        }

        var totalCount = await products.CountAsync(cancellationToken);

        var sortKey = query.SortBy?.Trim().ToLowerInvariant();
        products = sortKey switch
        {
            "oldest" => products.OrderBy(p => p.CreatedAt),
            _ => products.OrderByDescending(p => p.CreatedAt)
        };

        var rows = await products
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(p => new
            {
                p.Id,
                p.SellerId,
                p.CategoryId,
                p.Name,
                p.Brand,
                p.Status,
                p.CreatedAt,
                p.PublishedAt,
                SkuCode = p.Variants.Where(v => !v.IsDeleted).Select(v => v.SkuCode).FirstOrDefault() ?? string.Empty,
                PrimaryImageUrl = p.Images
                    .Where(i => !i.IsDeleted && i.IsActive)
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.Url)
                    .FirstOrDefault() ?? DefaultImageUrl
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new AuctionProductListItemDto(
            r.Id,
            r.SellerId,
            r.CategoryId,
            r.Name,
            r.Brand,
            r.SkuCode,
            r.Status.ToString(),
            r.PrimaryImageUrl,
            r.CreatedAt,
            r.PublishedAt
        )).ToList();

        return new PagedData<AuctionProductListItemDto>(items, PaginationMetadata.Create(safePage, safePageSize, totalCount));
    }

    public async Task<AuctionProductDetailDto> GetAuctionDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

        if (product == null || product.SaleType != SaleType.Auction)
        {
            throw new EntityNotFoundException("AUCTION_NOT_FOUND", "AuctionProduct", id);
        }

        var variant = product.Variants.FirstOrDefault(v => !v.IsDeleted);
        var skuCode = variant?.SkuCode ?? string.Empty;
        var variantId = variant?.Id ?? Guid.Empty;

        var images = product.Images
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.SortOrder)
            .Select(i => new ProductImageDto(i.Id, i.ProductId, i.VariantId, i.Url, i.AltText, i.IsActive, i.SortOrder))
            .ToList();

        return new AuctionProductDetailDto(
            ProductId: product.Id,
            SellerId: product.SellerId,
            CategoryId: product.CategoryId,
            CategoryName: product.Category?.Name ?? string.Empty,
            Name: product.Name,
            Description: product.Description,
            Brand: product.Brand,
            Status: product.Status.ToString(),
            SkuCode: skuCode,
            VariantId: variantId,
            Specifications: ProductSpecificationHelper.Deserialize(product.Specifications),
            Images: images,
            CreatedAt: product.CreatedAt,
            PublishedAt: product.PublishedAt,
            WeightGrams: product.WeightGrams ?? variant?.WeightGrams
        );
    }

    public async Task<PagedData<AuctionProductListItemDto>> SearchSellerAuctionsAsync(
        Guid sellerId,
        AuctionListQuery query,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = PaginationConstants.Normalize(query.Page, query.PageSize);

        var products = _dbContext.Products
            .AsNoTracking()
            .Where(p => p.SellerId == sellerId && !p.IsDeleted && p.SaleType == SaleType.Auction);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!string.Equals(query.Status, "all", StringComparison.OrdinalIgnoreCase) &&
                Enum.TryParse<ProductStatus>(query.Status, true, out var filterStatus))
            {
                products = products.Where(p => p.Status == filterStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim().ToLower();
            products = products.Where(p => p.Name.ToLower().Contains(keyword) || (p.Brand != null && p.Brand.ToLower().Contains(keyword)));
        }

        if (query.CategoryId.HasValue)
        {
            products = products.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        var totalCount = await products.CountAsync(cancellationToken);

        var sortKey = query.SortBy?.Trim().ToLowerInvariant();
        products = sortKey switch
        {
            "oldest" => products.OrderBy(p => p.CreatedAt),
            _ => products.OrderByDescending(p => p.CreatedAt)
        };

        var rows = await products
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(p => new
            {
                p.Id,
                p.SellerId,
                p.CategoryId,
                p.Name,
                p.Brand,
                p.Status,
                p.CreatedAt,
                p.PublishedAt,
                SkuCode = p.Variants.Where(v => !v.IsDeleted).Select(v => v.SkuCode).FirstOrDefault() ?? string.Empty,
                PrimaryImageUrl = p.Images
                    .Where(i => !i.IsDeleted && i.IsActive)
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.Url)
                    .FirstOrDefault() ?? DefaultImageUrl
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new AuctionProductListItemDto(
            r.Id,
            r.SellerId,
            r.CategoryId,
            r.Name,
            r.Brand,
            r.SkuCode,
            r.Status.ToString(),
            r.PrimaryImageUrl,
            r.CreatedAt,
            r.PublishedAt
        )).ToList();

        return new PagedData<AuctionProductListItemDto>(items, PaginationMetadata.Create(safePage, safePageSize, totalCount));
    }

    public async Task<ProductDto> ChangeAuctionStatusAsync(
        Guid id,
        ChangeProductStatusRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", id);

        if (product.SaleType != SaleType.Auction)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "This operation is only valid for auction products.");
        }

        return await ChangeStatusAsync(id, request, currentUserId, cancellationToken: cancellationToken);
    }

    // -------------------------------------------------------------
    // Admin Operations (CAT-50, CAT-51)
    // -------------------------------------------------------------

    public async Task<PagedData<AdminProductListItemDto>> SearchAdminProductsAsync(
        AdminProductSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = PaginationConstants.Normalize(query.Page, query.PageSize);

        var products = _dbContext.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted);

        if (query.SellerId.HasValue)
        {
            products = products.Where(p => p.SellerId == query.SellerId.Value);
        }

        if (query.Status.HasValue)
        {
            products = products.Where(p => p.Status == query.Status.Value);
        }

        if (query.StatusReason.HasValue)
        {
            products = products.Where(p => p.StatusReason == query.StatusReason.Value);
        }

        if (query.CategoryId.HasValue)
        {
            products = products.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        if (query.SaleType.HasValue)
        {
            products = products.Where(p => p.SaleType == query.SaleType.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim().ToLower();
            products = products.Where(p => p.Name.ToLower().Contains(keyword) || (p.Brand != null && p.Brand.ToLower().Contains(keyword)));
        }

        var totalCount = await products.CountAsync(cancellationToken);

        var isDesc = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        var sortKey = query.SortBy?.Trim().ToLowerInvariant();

        products = (sortKey, isDesc) switch
        {
            ("name", false) => products.OrderBy(p => p.Name).ThenBy(p => p.Id),
            ("name", true) => products.OrderByDescending(p => p.Name).ThenBy(p => p.Id),
            ("status", false) => products.OrderBy(p => p.Status).ThenBy(p => p.Id),
            ("status", true) => products.OrderByDescending(p => p.Status).ThenBy(p => p.Id),
            ("createdat", false) => products.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id),
            _ => products.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
        };

        var rows = await products
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(p => new
            {
                p.Id,
                p.SellerId,
                p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty,
                p.Name,
                p.Brand,
                SaleType = p.SaleType.ToString(),
                Status = p.Status.ToString(),
                StatusReason = p.StatusReason != null ? p.StatusReason.ToString() : null,
                MinPrice = p.Variants.Where(v => !v.IsDeleted && v.Status == VariantStatus.Active).Min(v => v.Price),
                MaxPrice = p.Variants.Where(v => !v.IsDeleted && v.Status == VariantStatus.Active).Max(v => v.Price),
                PrimaryImageUrl = p.Images
                    .Where(i => !i.IsDeleted && i.IsActive)
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.Url)
                    .FirstOrDefault() ?? DefaultImageUrl,
                p.CreatedAt,
                p.PublishedAt
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new AdminProductListItemDto(
            r.Id,
            r.SellerId,
            r.CategoryId,
            r.CategoryName,
            r.Name,
            r.Brand,
            r.SaleType,
            r.Status,
            r.StatusReason,
            r.MinPrice,
            r.MaxPrice,
            r.PrimaryImageUrl,
            r.CreatedAt,
            r.PublishedAt
        )).ToList();

        return new PagedData<AdminProductListItemDto>(items, PaginationMetadata.Create(safePage, safePageSize, totalCount));
    }

    public async Task<ProductDto> ModerateProductAsync(
        Guid id,
        ModerateProductRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Action))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Action is required. Allowed values: APPROVE, SUSPEND, REJECT, UNLOCK, BAN.");
        }

        var product = await _dbContext.Products
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", id);

        if (product.Status == ProductStatus.Auctioned)
        {
            throw new BusinessRuleValidationException("PRODUCT_ALREADY_AUCTIONED", "Auctioned products are permanently locked and cannot be moderated.");
        }

        var action = request.Action.Trim().ToUpperInvariant();

        if (product.Status == ProductStatus.Banned && action != "BAN")
        {
            throw new BusinessRuleValidationException("PRODUCT_ALREADY_BANNED", "This product has already been permanently banned and cannot be re-activated.");
        }

        var oldStatus = product.Status;
        var oldReason = product.StatusReason;
        var now = DateTimeOffset.UtcNow;

        if (action == "APPROVE" || action == "UNLOCK")
        {
            var hasActiveVariant = product.Variants.Any(v => !v.IsDeleted && v.Status == VariantStatus.Active && (product.SaleType == SaleType.Auction || (v.Price.HasValue && v.Price.Value > 0)));
            if (!hasActiveVariant)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot approve product without at least one active variant with valid price.");
            }

            var hasActiveImage = product.Images.Any(i => !i.IsDeleted && i.IsActive);
            if (!hasActiveImage)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot approve product without at least one active product image.");
            }

            if (product.SaleType == SaleType.Auction)
            {
                if (!product.WeightGrams.HasValue || product.WeightGrams.Value <= 0)
                {
                    throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot approve auction product without valid shipping weight. Product must declare WeightGrams greater than 0.");
                }
            }
            else
            {
                var productHasWeight = product.WeightGrams.HasValue && product.WeightGrams.Value > 0;
                if (!productHasWeight)
                {
                    var variantsMissingWeight = product.Variants
                        .Where(v => !v.IsDeleted && v.Status == VariantStatus.Active)
                        .Any(v => !v.WeightGrams.HasValue || v.WeightGrams.Value <= 0);

                    if (variantsMissingWeight)
                    {
                        throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot approve product without valid shipping weight. Either declare WeightGrams on product or provide WeightGrams greater than 0 for all active variants.");
                    }
                }
            }

            product.Status = ProductStatus.Active;
            product.StatusReason = null;
            product.PublishedAt ??= now;

            // Activate valid variants
            foreach (var v in product.Variants.Where(v => !v.IsDeleted && (product.SaleType == SaleType.Auction || (v.Price.HasValue && v.Price.Value > 0))))
            {
                v.Status = VariantStatus.Active;
            }

            // Initialize physical warehouse inventory in FulfillmentService upon Admin/Staff approval
            if (oldStatus != ProductStatus.Active && product.SaleType == SaleType.Regular)
            {
                var activeVariants = product.Variants
                    .Where(v => !v.IsDeleted && v.Status == VariantStatus.Active)
                    .ToList();

                foreach (var variant in activeVariants)
                {
                    try
                    {
                        await _fulfillmentClient.InitializeInventoryForSkuAsync(
                            variant.Id,
                            product.SellerId,
                            initialQuantity: 0,
                            cancellationToken: cancellationToken);
                    }
                    catch
                    {
                        // Non-blocking: Fulfillment logging or connection failure should not abort product approval
                    }
                }
            }
        }
        else if (action == "REJECT")
        {
            product.Status = ProductStatus.Draft;
            product.StatusReason = ProductInactiveReason.Rejected;
        }
        else if (action == "SUSPEND")
        {
            product.Status = ProductStatus.Inactive;
            product.StatusReason = ProductInactiveReason.AdminLocked;

            // Deactivate variants
            foreach (var v in product.Variants.Where(v => !v.IsDeleted))
            {
                v.Status = VariantStatus.Inactive;
            }
        }
        else if (action == "BAN")
        {
            product.Status = ProductStatus.Banned;
            product.StatusReason = null;

            // Deactivate variants
            foreach (var v in product.Variants.Where(v => !v.IsDeleted))
            {
                v.Status = VariantStatus.Inactive;
                v.IsActive = false;
            }

            // Auto-resolve pending/investigating reports for this product
            var pendingReports = await _dbContext.ProductReports
                .Where(r => r.ProductId == product.Id 
                         && (r.Status == ReportStatus.Pending || r.Status == ReportStatus.Investigating)
                         && !r.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var r in pendingReports)
            {
                r.Status = ReportStatus.Resolved;
                r.ReviewedBy = adminId;
                r.ReviewedAt = now;
                r.ResolutionNote = "Product permanently banned by administration: " + request.Note;
                r.UpdatedAt = now;
            }
        }
        else
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", $"Unknown action '{request.Action}'. Allowed values: APPROVE, SUSPEND, REJECT, UNLOCK, BAN.");
        }

        product.UpdatedAt = now;
        product.UpdatedBy = adminId;

        var auditAction = action == "BAN" ? AuditAction.ProductBan : AuditAction.ProductUpdate;

        _dbContext.AddAuditLog(
            auditAction,
            "Product",
            product.Id.ToString(),
            oldValues: new { Status = oldStatus.ToString(), Reason = oldReason?.ToString() },
            newValues: new { Status = product.Status.ToString(), Reason = product.StatusReason?.ToString(), Note = request.Note, ModeratedBy = adminId },
            userId: adminId.ToString());

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Deduct 10 reputation points from Seller when Product is banned due to violations (P2-06)
        if (action == "BAN" && product.SellerId != Guid.Empty)
        {
            await _userClient.PenalizeSellerAsync(
                product.SellerId,
                penaltyPoints: 10,
                reason: "PRODUCT_BANNED_SELLER",
                referenceId: product.Id,
                cancellationToken: cancellationToken);
        }

        // Send In-App notification to Seller regarding product moderation decision
        if (product.SellerId != Guid.Empty)
        {
            var reasonText = request.Reason ?? request.Note ?? "Không có lý do chi tiết";
            switch (action)
            {
                case "APPROVE":
                case "UNLOCK":
                    await _notificationClient.SendInAppNotificationAsync(
                        product.SellerId,
                        "Sản phẩm đã được phê duyệt thành công",
                        $"Sản phẩm '{product.Name}' của bạn đã được kiểm duyệt đạt chuẩn và chính thức mở bán trên sàn.",
                        "PRODUCT_APPROVED",
                        $"{{\"productId\":\"{product.Id}\",\"status\":\"Active\"}}",
                        cancellationToken);
                    break;
                case "REJECT":
                    await _notificationClient.SendInAppNotificationAsync(
                        product.SellerId,
                        "Sản phẩm bị từ chối phê duyệt",
                        $"Sản phẩm '{product.Name}' chưa đạt yêu cầu kiểm duyệt. Lý do: {reasonText}. Vui lòng cập nhật lại thông tin.",
                        "PRODUCT_REJECTED",
                        $"{{\"productId\":\"{product.Id}\",\"reason\":\"{reasonText}\",\"status\":\"Draft\"}}",
                        cancellationToken);
                    break;
                case "SUSPEND":
                    await _notificationClient.SendInAppNotificationAsync(
                        product.SellerId,
                        "Sản phẩm bị tạm dừng hiển thị",
                        $"Sản phẩm '{product.Name}' đã bị tạm khóa do nghi vấn vi phạm hoặc chờ bổ sung chứng từ. Lý do: {reasonText}.",
                        "PRODUCT_SUSPENDED",
                        $"{{\"productId\":\"{product.Id}\",\"reason\":\"{reasonText}\",\"status\":\"Inactive\"}}",
                        cancellationToken);
                    break;
                case "BAN":
                    await _notificationClient.SendInAppNotificationAsync(
                        product.SellerId,
                        "CẢNH BÁO: Sản phẩm bị cấm vĩnh viễn và trừ 10 điểm uy tín",
                        $"Sản phẩm '{product.Name}' đã bị gỡ bỏ vĩnh viễn khỏi hệ thống do vi phạm nghiêm trọng chính sách sàn. Tài khoản Shop bị phạt trừ 10 điểm uy tín. Lý do: {reasonText}.",
                        "PRODUCT_BANNED",
                        $"{{\"productId\":\"{product.Id}\",\"penaltyPoints\":10,\"status\":\"Banned\"}}",
                        cancellationToken);
                    break;
            }
        }

        return new ProductDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Brand,
            product.Status.ToString(),
            product.WeightGrams);
    }

    // -------------------------------------------------------------
    // Inter-Service Auction Operations
    // -------------------------------------------------------------

    public async Task<AuctionEligibilityResponse> CheckAuctionEligibilityAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

        if (product == null)
        {
            return new AuctionEligibilityResponse(
                ProductId: id,
                IsEligible: false,
                Reason: "Product was not found or is deleted.",
                Status: "NotFound",
                SkuCode: null,
                WeightGrams: null);
        }

        if (product.SaleType != SaleType.Auction)
        {
            return new AuctionEligibilityResponse(
                ProductId: id,
                IsEligible: false,
                Reason: "Product is not registered as an auction product.",
                Status: product.Status.ToString(),
                SkuCode: product.Variants.FirstOrDefault()?.SkuCode,
                WeightGrams: product.WeightGrams);
        }

        if (product.Status == ProductStatus.Banned)
        {
            return new AuctionEligibilityResponse(
                ProductId: id,
                IsEligible: false,
                Reason: "Product has been permanently banned.",
                Status: product.Status.ToString(),
                SkuCode: product.Variants.FirstOrDefault()?.SkuCode,
                WeightGrams: product.WeightGrams);
        }

        if (product.Status == ProductStatus.InAuction)
        {
            return new AuctionEligibilityResponse(
                ProductId: id,
                IsEligible: false,
                Reason: "Product is already locked in an ongoing auction.",
                Status: product.Status.ToString(),
                SkuCode: product.Variants.FirstOrDefault()?.SkuCode,
                WeightGrams: product.WeightGrams);
        }

        if (product.Status == ProductStatus.Auctioned)
        {
            return new AuctionEligibilityResponse(
                ProductId: id,
                IsEligible: false,
                Reason: "Product has already been auctioned and cannot be put into auction again.",
                Status: product.Status.ToString(),
                SkuCode: product.Variants.FirstOrDefault()?.SkuCode,
                WeightGrams: product.WeightGrams);
        }

        if (product.Status != ProductStatus.Active)
        {
            return new AuctionEligibilityResponse(
                ProductId: id,
                IsEligible: false,
                Reason: $"Product status is '{product.Status}'. Only active products can be placed into an auction.",
                Status: product.Status.ToString(),
                SkuCode: product.Variants.FirstOrDefault()?.SkuCode,
                WeightGrams: product.WeightGrams);
        }

        var hasActiveImage = product.Images.Any(i => !i.IsDeleted && i.IsActive);
        if (!hasActiveImage)
        {
            return new AuctionEligibilityResponse(
                ProductId: id,
                IsEligible: false,
                Reason: "Product does not have any active images.",
                Status: product.Status.ToString(),
                SkuCode: product.Variants.FirstOrDefault()?.SkuCode,
                WeightGrams: product.WeightGrams);
        }

        return new AuctionEligibilityResponse(
            ProductId: id,
            IsEligible: true,
            Reason: null,
            Status: product.Status.ToString(),
            SkuCode: product.Variants.FirstOrDefault()?.SkuCode,
            WeightGrams: product.WeightGrams);
    }

    public async Task<ProductDto> LockForAuctionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", id);

        if (product.SaleType != SaleType.Auction)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "This operation is only valid for auction products.");
        }

        if (product.Status == ProductStatus.InAuction)
        {
            // Idempotent: already locked in an active auction
            return new ProductDto(
                product.Id,
                product.SellerId,
                product.CategoryId,
                product.Name,
                product.Description,
                product.Brand,
                product.Status.ToString(),
                product.WeightGrams);
        }

        if (product.Status == ProductStatus.Auctioned)
        {
            throw new BusinessRuleValidationException("PRODUCT_ALREADY_AUCTIONED", "Auctioned products cannot be put into auction again.");
        }

        if (product.Status != ProductStatus.Active)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", $"Cannot lock product with status '{product.Status}' for auction. Product must be in Active status.");
        }

        var oldStatus = product.Status;
        product.Status = ProductStatus.InAuction;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        _dbContext.AddAuditLog(
            AuditAction.ProductUpdate,
            "Product",
            product.Id.ToString(),
            oldValues: new { Status = oldStatus },
            newValues: new { Status = product.Status },
            userId: "System:AuctionService");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProductDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Brand,
            product.Status.ToString(),
            product.WeightGrams);
    }

    public async Task<ProductDto> UnlockFromAuctionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", id);

        if (product.SaleType != SaleType.Auction)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "This operation is only valid for auction products.");
        }

        if (product.Status == ProductStatus.Active)
        {
            // Idempotent: already active / unlocked
            return new ProductDto(
                product.Id,
                product.SellerId,
                product.CategoryId,
                product.Name,
                product.Description,
                product.Brand,
                product.Status.ToString(),
                product.WeightGrams);
        }

        if (product.Status != ProductStatus.InAuction)
        {
            throw new BusinessRuleValidationException("PRODUCT_NOT_IN_AUCTION", $"Product is not currently in an active auction. Current status is '{product.Status}'.");
        }

        var oldStatus = product.Status;
        product.Status = ProductStatus.Active;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        _dbContext.AddAuditLog(
            AuditAction.ProductUpdate,
            "Product",
            product.Id.ToString(),
            oldValues: new { Status = oldStatus },
            newValues: new { Status = product.Status },
            userId: "System:AuctionService");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProductDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Brand,
            product.Status.ToString(),
            product.WeightGrams);
    }

    public async Task<ProductDto> MarkAsAuctionedAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", id);

        if (product.SaleType != SaleType.Auction)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "This operation is only valid for auction products.");
        }

        if (product.Status == ProductStatus.Auctioned)
        {
            // Idempotent: already marked as auctioned
            return new ProductDto(
                product.Id,
                product.SellerId,
                product.CategoryId,
                product.Name,
                product.Description,
                product.Brand,
                product.Status.ToString(),
                product.WeightGrams);
        }

        var oldStatus = product.Status;
        product.Status = ProductStatus.Auctioned;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        _dbContext.AddAuditLog(
            AuditAction.ProductUpdate,
            "Product",
            product.Id.ToString(),
            oldValues: new { Status = oldStatus },
            newValues: new { Status = product.Status },
            userId: "System:AuctionService");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProductDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Brand,
            product.Status.ToString(),
            product.WeightGrams);
    }

    public async Task<ProductDto> RevertAuctionedAsync(
        Guid id,
        RevertAuctionedRequest? request,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", id);

        if (product.SaleType != SaleType.Auction)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "This operation is only valid for auction products.");
        }

        if (product.Status == ProductStatus.Active)
        {
            // Idempotent: already reverted back to Active
            return new ProductDto(
                product.Id,
                product.SellerId,
                product.CategoryId,
                product.Name,
                product.Description,
                product.Brand,
                product.Status.ToString(),
                product.WeightGrams);
        }

        if (product.Status != ProductStatus.Auctioned)
        {
            throw new BusinessRuleValidationException("PRODUCT_NOT_AUCTIONED", $"Product is not in Auctioned status. Current status is '{product.Status}'.");
        }

        var oldStatus = product.Status;
        product.Status = ProductStatus.Active;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        var reason = !string.IsNullOrWhiteSpace(request?.Reason) ? request.Reason.Trim() : "SELLER_REJECTED";
        var note = request?.Note?.Trim();

        _dbContext.AddAuditLog(
            AuditAction.ProductUpdate,
            "Product",
            product.Id.ToString(),
            oldValues: new { Status = oldStatus },
            newValues: new { Status = product.Status, Reason = reason, Note = note },
            userId: "System:AuctionService");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProductDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Brand,
            product.Status.ToString(),
            product.WeightGrams);
    }
}
