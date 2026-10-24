namespace Catalog.Api.Mock;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Catalog.Application.Services.Category;
using Catalog.Application.Services.Product;

public sealed record InventoryDto(
    Guid ProductId,
    int Available,
    int Reserved,
    DateTimeOffset UpdatedAt);

public sealed class MockCatalogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly List<CategoryDto> _categories = new();
    private readonly List<ProductDto> _products = new();
    private readonly Dictionary<Guid, InventoryDto> _inventories = new();
    private readonly object _lock = new();

    public MockCatalogStore()
    {
        LoadSeedData();
    }

    private void LoadSeedData()
    {
        try
        {
            var seedsDir = Path.Combine(AppContext.BaseDirectory, "Seeds");
            if (!Directory.Exists(seedsDir))
            {
                seedsDir = Path.Combine(Directory.GetCurrentDirectory(), "Seeds");
            }

            var catPath = Path.Combine(seedsDir, "categories.json");
            if (File.Exists(catPath))
            {
                var catJson = File.ReadAllText(catPath);
                var loaded = JsonSerializer.Deserialize<List<CategoryDto>>(catJson, JsonOptions);
                if (loaded != null) _categories.AddRange(loaded);
            }

            var prodPath = Path.Combine(seedsDir, "products.json");
            if (File.Exists(prodPath))
            {
                var prodJson = File.ReadAllText(prodPath);
                var loaded = JsonSerializer.Deserialize<List<ProductDto>>(prodJson, JsonOptions);
                if (loaded != null) _products.AddRange(loaded);
            }
        }
        catch
        {
            // Fallback default in-memory data
        }

        if (_categories.Count == 0)
        {
            _categories.Add(new CategoryDto(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), null, "Thiết Bị Điện Tử", "Điện thoại, máy tính, phụ kiện công nghệ", 1, 1, false));
            _categories.Add(new CategoryDto(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), null, "Thời Trang & Phụ Kiện", "Quần áo, giày dép, phụ kiện", 1, 2, false));
        }

        if (_products.Count == 0)
        {
            _products.Add(new ProductDto(
                Guid.Parse("22222222-2222-2222-2222-000000000001"),
                Guid.Parse("11111111-1111-1111-1111-000000000004"),
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "Bàn phím cơ không dây Nexus Pro Wireless",
                "Bàn phím cơ 3 modes kết nối (Type-C, 2.4G, Bluetooth 5.0)",
                "Nexus Tech",
                "Active"
            ));
        }

        foreach (var p in _products)
        {
            _inventories[p.Id] = new InventoryDto(p.Id, 100, 5, DateTimeOffset.UtcNow);
        }
    }

    // --- CATEGORIES ---
    public IReadOnlyList<CategoryDto> GetCategories()
    {
        lock (_lock) return _categories.ToList();
    }

    public CategoryDto? GetCategoryById(Guid id)
    {
        lock (_lock) return _categories.FirstOrDefault(c => c.Id == id);
    }

    public CategoryDto CreateCategory(string name, string? description, Guid? parentId, short level, int sortOrder)
    {
        lock (_lock)
        {
            var newCat = new CategoryDto(Guid.NewGuid(), parentId, name, description, level, sortOrder, false);
            _categories.Add(newCat);
            return newCat;
        }
    }

    public CategoryDto? UpdateCategory(Guid id, string name, string? description, short level, int sortOrder, bool isDisabled)
    {
        lock (_lock)
        {
            var idx = _categories.FindIndex(c => c.Id == id);
            if (idx == -1) return null;
            var current = _categories[idx];
            var updated = new CategoryDto(id, current.ParentId, name, description, level, sortOrder, isDisabled);
            _categories[idx] = updated;
            return updated;
        }
    }

    public bool DeleteCategory(Guid id)
    {
        lock (_lock)
        {
            var count = _categories.RemoveAll(c => c.Id == id);
            return count > 0;
        }
    }

    // --- PRODUCTS ---
    public (IReadOnlyList<ProductDto> Items, int TotalCount) GetProducts(int page = 1, int pageSize = 20, Guid? categoryId = null, Guid? sellerId = null)
    {
        lock (_lock)
        {
            var query = _products.AsEnumerable();
            if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
            if (sellerId.HasValue) query = query.Where(p => p.SellerId == sellerId.Value);

            var total = query.Count();
            var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return (items, total);
        }
    }

    public ProductDto? GetProductById(Guid id)
    {
        lock (_lock) return _products.FirstOrDefault(p => p.Id == id);
    }

    public ProductDto CreateProduct(Guid sellerId, Guid categoryId, string name, string? description, string? brand)
    {
        lock (_lock)
        {
            var newProd = new ProductDto(Guid.NewGuid(), sellerId, categoryId, name, description, brand, "Active");
            _products.Add(newProd);
            _inventories[newProd.Id] = new InventoryDto(newProd.Id, 50, 0, DateTimeOffset.UtcNow);
            return newProd;
        }
    }

    public ProductDto? UpdateProduct(Guid id, string name, string? description, string? brand, string status)
    {
        lock (_lock)
        {
            var idx = _products.FindIndex(p => p.Id == id);
            if (idx == -1) return null;
            var current = _products[idx];
            var updated = new ProductDto(id, current.SellerId, current.CategoryId, name, description, brand, status);
            _products[idx] = updated;
            return updated;
        }
    }

    public bool DeleteProduct(Guid id)
    {
        lock (_lock)
        {
            var count = _products.RemoveAll(p => p.Id == id);
            _inventories.Remove(id);
            return count > 0;
        }
    }

    // --- INVENTORY ---
    public InventoryDto? GetInventory(Guid productId)
    {
        lock (_lock)
        {
            if (_inventories.TryGetValue(productId, out var inv)) return inv;
            return new InventoryDto(productId, 100, 0, DateTimeOffset.UtcNow);
        }
    }

    public InventoryDto AdjustInventory(Guid productId, int quantityChange)
    {
        lock (_lock)
        {
            if (!_inventories.TryGetValue(productId, out var current))
            {
                current = new InventoryDto(productId, 100, 0, DateTimeOffset.UtcNow);
            }
            var newAvailable = Math.Max(0, current.Available + quantityChange);
            var updated = new InventoryDto(productId, newAvailable, current.Reserved, DateTimeOffset.UtcNow);
            _inventories[productId] = updated;
            return updated;
        }
    }
}
