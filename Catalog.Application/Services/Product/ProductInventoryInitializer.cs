namespace Catalog.Application.Services.Product;

using Catalog.Application.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Creates the inventory row in FulfillmentService for each SKU of an active Regular product.
/// A failed call leaves the SKU Pending with a backoff, and the background worker retries it. After <see cref="MaxAttempts"/>
/// failures the SKU is flagged NeedsAttention for the seller, but is still retried every <see cref="AttentionRetryInterval"/>,
/// so it recovers by itself once the cause (for example a missing seller warehouse) is fixed.
/// </summary>
public interface IProductInventoryInitializer
{
    /// <summary>
    /// Tries the Fulfillment call now for the given SKUs, which the caller has just saved as Pending, and saves the outcome.
    /// It never throws for a Fulfillment failure; whatever is not done stays Pending for the worker.
    /// </summary>
    Task TryInitializeAsync(IReadOnlyCollection<ProductVariant> variants, Guid sellerId, CancellationToken cancellationToken = default);

    /// <summary>Retries SKUs whose retry time has come. Returns how many SKUs this instance attempted.</summary>
    Task<int> ProcessDueAsync(int batchSize, CancellationToken cancellationToken = default);
}

public sealed class ProductInventoryInitializer : IProductInventoryInitializer
{
    public const int MaxAttempts = 10;

    /// <summary>How long a SKU is reserved for the caller that is initializing it. Longer than one Fulfillment call.</summary>
    public static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);

    /// <summary>Slow retry for a SKU that needs attention, so it is not stuck after its cause is fixed.</summary>
    public static readonly TimeSpan AttentionRetryInterval = TimeSpan.FromHours(6);

    private readonly ICatalogDbContext _dbContext;
    private readonly IFulfillmentClient _fulfillmentClient;
    private readonly ILogger<ProductInventoryInitializer> _logger;

    public ProductInventoryInitializer(
        ICatalogDbContext dbContext,
        IFulfillmentClient fulfillmentClient,
        ILogger<ProductInventoryInitializer> logger)
    {
        _dbContext = dbContext;
        _fulfillmentClient = fulfillmentClient;
        _logger = logger;
    }

    /// <summary>
    /// Marks a SKU as needing its inventory row. The caller saves it together with the approval and then calls
    /// <see cref="TryInitializeAsync"/>. The retry time starts one lease away, so the worker leaves the SKU to that call
    /// and only picks it up if the call never finishes (for example the process stopped).
    /// </summary>
    public static void MarkPending(ProductVariant variant, DateTimeOffset now)
    {
        if (variant.InventoryInitState == InventoryInitState.Done)
            return;

        variant.InventoryInitState = InventoryInitState.Pending;
        variant.InventoryInitAttemptCount = 0;
        variant.InventoryInitNextRetryAt = now.Add(ClaimLease);
        variant.InventoryInitLastError = null;
    }

    public async Task TryInitializeAsync(
        IReadOnlyCollection<ProductVariant> variants, Guid sellerId, CancellationToken cancellationToken = default)
    {
        var pending = variants.Where(v => v.InventoryInitState == InventoryInitState.Pending).ToList();
        if (pending.Count == 0)
            return;

        try
        {
            await InitializeForSellerAsync(pending, sellerId, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is DbUpdateException or OperationCanceledException)
        {
            // The approval is already saved and the Fulfillment call is idempotent, so the worker finishes this after the lease.
            _logger.LogWarning(ex, "Inventory initialization for seller {SellerId} did not finish. The worker will retry.", sellerId);
        }
    }

    public async Task<int> ProcessDueAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var candidates = await _dbContext.ProductVariants
            .Include(v => v.Product)
            .Where(v => (v.InventoryInitState == InventoryInitState.Pending || v.InventoryInitState == InventoryInitState.NeedsAttention)
                        && !v.IsDeleted
                        && (v.InventoryInitNextRetryAt == null || v.InventoryInitNextRetryAt <= now))
            .OrderBy(v => v.InventoryInitNextRetryAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var claimed = new List<ProductVariant>();
        foreach (var variant in candidates)
        {
            if (await TryClaimAsync(variant, now, cancellationToken))
                claimed.Add(variant);
        }

        foreach (var variant in claimed.Where(v => !NeedsInventory(v.Product)))
        {
            // Nothing to create any more (for example the product was deleted or became an auction product).
            variant.InventoryInitState = InventoryInitState.NotRequired;
            variant.InventoryInitNextRetryAt = null;
        }

        foreach (var group in claimed.Where(v => NeedsInventory(v.Product)).GroupBy(v => v.Product!.SellerId))
            await InitializeForSellerAsync(group.ToList(), group.Key, cancellationToken);

        if (claimed.Count > 0)
            await _dbContext.SaveChangesAsync(cancellationToken);

        return claimed.Count;
    }

    private static bool NeedsInventory(Product? product) =>
        product is not null && !product.IsDeleted && product.SaleType == SaleType.Regular;

    /// <summary>
    /// Takes the SKU for this instance by moving its retry time one lease ahead, only if no other instance moved it first.
    /// The conditional UPDATE is atomic in the database, so two instances never initialize the same SKU at the same time.
    /// </summary>
    private async Task<bool> TryClaimAsync(ProductVariant variant, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var observed = variant.InventoryInitNextRetryAt;
        var leaseUntil = now.Add(ClaimLease);

        if (_dbContext.Database.IsRelational())
        {
            var updated = await _dbContext.ProductVariants
                .Where(v => v.Id == variant.Id
                            && v.InventoryInitState == variant.InventoryInitState
                            && v.InventoryInitNextRetryAt == observed)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.InventoryInitNextRetryAt, leaseUntil), cancellationToken);
            if (updated == 0)
                return false;
        }

        // Keep the tracked copy in line with the row. A provider without set-based updates (the in-memory test database)
        // has a single process, so the plain assignment is enough there.
        variant.InventoryInitNextRetryAt = leaseUntil;
        return true;
    }

    /// <summary>Looks up the seller's warehouse once and declares an inventory row for each SKU in it.</summary>
    private async Task InitializeForSellerAsync(IReadOnlyList<ProductVariant> variants, Guid sellerId, CancellationToken cancellationToken)
    {
        Guid? warehouseId = null;
        string? lookupError = null;
        if (sellerId == Guid.Empty)
        {
            lookupError = "The product has no seller, so no warehouse can hold its inventory.";
        }
        else
        {
            try
            {
                var warehouses = await _fulfillmentClient.GetWarehousesBySellerIdAsync(sellerId, cancellationToken);
                warehouseId = warehouses.FirstOrDefault(w => w.IsActive)?.Id;
                if (warehouseId is null)
                    lookupError = "The seller has no active warehouse, or Fulfillment could not be reached.";
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                lookupError = ex.Message;
            }
        }

        foreach (var variant in variants)
        {
            var error = lookupError;
            if (error is null)
            {
                try
                {
                    var result = await _fulfillmentClient.DeclareInventoryAsync(
                        new DeclareInventoryClientRequest(variant.Id, warehouseId!.Value, 0, "Initial inventory declared by CatalogService"),
                        cancellationToken);
                    if (result is null)
                        error = "Fulfillment did not create the inventory row.";
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    error = ex.Message;
                }
            }

            RecordOutcome(variant, error);
        }
    }

    private void RecordOutcome(ProductVariant variant, string? error)
    {
        var now = DateTimeOffset.UtcNow;
        if (error is null)
        {
            variant.InventoryInitState = InventoryInitState.Done;
            variant.InventoryInitNextRetryAt = null;
            variant.InventoryInitLastError = null;
            return;
        }

        variant.InventoryInitAttemptCount += 1;
        variant.InventoryInitLastError = error.Length > 500 ? error[..500] : error;
        if (variant.InventoryInitAttemptCount >= MaxAttempts)
        {
            if (variant.InventoryInitState != InventoryInitState.NeedsAttention)
            {
                _logger.LogError(
                    "Inventory initialization for SKU {SkuId} failed {Attempts} times and needs attention: {Error}",
                    variant.Id, variant.InventoryInitAttemptCount, variant.InventoryInitLastError);
            }
            variant.InventoryInitState = InventoryInitState.NeedsAttention;
            variant.InventoryInitNextRetryAt = now.Add(AttentionRetryInterval);
            return;
        }

        variant.InventoryInitNextRetryAt = now.Add(Backoff(variant.InventoryInitAttemptCount));
        _logger.LogWarning(
            "Inventory initialization for SKU {SkuId} failed (attempt {Attempt}). Retrying at {NextRetryAt}.",
            variant.Id, variant.InventoryInitAttemptCount, variant.InventoryInitNextRetryAt);
    }

    private static TimeSpan Backoff(int attempt) => attempt switch
    {
        <= 1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(2),
        3 => TimeSpan.FromMinutes(5),
        4 => TimeSpan.FromMinutes(10),
        _ => TimeSpan.FromMinutes(30)
    };
}
