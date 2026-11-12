namespace Catalog.Application.Services.Report;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Catalog.Application.Common.Extensions;
using Catalog.Application.Common.Models;
using Catalog.Application.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Enums;
using Catalog.Domain.Exceptions;

public sealed class ProductReportService : IProductReportService
{
    private readonly ICatalogDbContext _dbContext;
    private readonly IImageStorageService _imageStorageService;
    private readonly IUserClient _userClient;
    private readonly INotificationClient _notificationClient;

    public ProductReportService(
        ICatalogDbContext dbContext,
        IImageStorageService imageStorageService,
        IUserClient userClient,
        INotificationClient notificationClient)
    {
        _dbContext = dbContext;
        _imageStorageService = imageStorageService;
        _userClient = userClient;
        _notificationClient = notificationClient;
    }

    public async Task<ProductReportDto> SubmitProductReportAsync(
        Guid productId,
        CreateProductReportRequest request,
        Guid reporterId,
        IReadOnlyList<FileUploadModel>? evidenceFiles = null,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Request body cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Report reason is required.");
        }

        if (!Enum.TryParse<ReportReason>(request.Reason, true, out var reportReason))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", $"Invalid report reason '{request.Reason}'. Allowed values: Counterfeit, ProhibitedItem, IncorrectDescription, OffensiveContent, Other.");
        }

        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", productId);

        if (product.Status == ProductStatus.Banned)
        {
            throw new BusinessRuleValidationException("PRODUCT_ALREADY_BANNED", "This product has already been permanently banned.");
        }

        // Anti-spam check: prevent duplicate active reports from the same reporter
        var hasActiveReport = await _dbContext.ProductReports
            .AsNoTracking()
            .AnyAsync(r => r.ProductId == productId 
                        && r.ReporterId == reporterId 
                        && (r.Status == ReportStatus.Pending || r.Status == ReportStatus.Investigating)
                        && !r.IsDeleted, cancellationToken);

        if (hasActiveReport)
        {
            throw new BusinessRuleValidationException("ALREADY_REPORTED", "You have already submitted a report for this product that is currently under review.", 409);
        }

        if (evidenceFiles == null || evidenceFiles.Count == 0)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "At least one evidence image is required to submit a report.");
        }

        if (evidenceFiles.Count > 5)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Maximum 5 evidence images are allowed.");
        }

        var evidenceUrls = new List<string>();
        foreach (var file in evidenceFiles)
        {
            var uploadResult = await _imageStorageService.UploadAsync(
                file.Stream,
                file.FileName,
                folder: "reports/evidence",
                cancellationToken: cancellationToken);

            if (!string.IsNullOrWhiteSpace(uploadResult?.Url))
            {
                evidenceUrls.Add(uploadResult.Url);
            }
        }

        string? evidenceJson = evidenceUrls.Count > 0 ? JsonSerializer.Serialize(evidenceUrls) : null;

        var now = DateTimeOffset.UtcNow;
        var report = new ProductReport
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ReporterId = reporterId,
            Reason = reportReason,
            Description = request.Description.Trim(),
            EvidenceImages = evidenceJson,
            Status = ReportStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };

        _dbContext.ProductReports.Add(report);

        _dbContext.AddAuditLog(
            AuditAction.ProductReport,
            "ProductReport",
            report.Id.ToString(),
            newValues: new { ProductId = productId, ReporterId = reporterId, Reason = reportReason.ToString(), Status = "Pending" },
            userId: reporterId.ToString());

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Send In-App notification to Buyer confirming report receipt
        await _notificationClient.SendInAppNotificationAsync(
            reporterId,
            "Đã tiếp nhận đơn tố cáo sản phẩm",
            $"Chúng tôi đã tiếp nhận đơn tố cáo của bạn đối với sản phẩm '{product.Name}'. Đội ngũ Quản trị viên sẽ tiến hành xác minh trong 24-48 giờ.",
            "REPORT_SUBMITTED",
            $"{{\"reportId\":\"{report.Id}\",\"productId\":\"{product.Id}\"}}",
            cancellationToken);

        return MapToDto(report);
    }

    public async Task<PagedData<ProductReportListItemDto>> GetAdminReportsAsync(
        AdminReportSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var safePage = query.Page < 1 ? 1 : query.Page;
        var safePageSize = query.PageSize < 1 ? 20 : (query.PageSize > 100 ? 100 : query.PageSize);

        var dbQuery = _dbContext.ProductReports
            .Include(r => r.Product)
            .AsNoTracking()
            .Where(r => !r.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (Enum.TryParse<ReportStatus>(query.Status, true, out var filterStatus))
            {
                dbQuery = dbQuery.Where(r => r.Status == filterStatus);
            }
            else
            {
                throw new BusinessRuleValidationException("VAL_INVALID_INPUT", $"Invalid status filter '{query.Status}'. Allowed values: Pending, Investigating, Resolved, Dismissed.");
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Reason))
        {
            if (Enum.TryParse<ReportReason>(query.Reason, true, out var filterReason))
            {
                dbQuery = dbQuery.Where(r => r.Reason == filterReason);
            }
            else
            {
                throw new BusinessRuleValidationException("VAL_INVALID_INPUT", $"Invalid reason filter '{query.Reason}'. Allowed values: Counterfeit, ProhibitedItem, IncorrectDescription, OffensiveContent, Other.");
            }
        }

        if (query.ProductId.HasValue && query.ProductId.Value != Guid.Empty)
        {
            dbQuery = dbQuery.Where(r => r.ProductId == query.ProductId.Value);
        }

        if (query.SellerId.HasValue && query.SellerId.Value != Guid.Empty)
        {
            dbQuery = dbQuery.Where(r => r.Product != null && r.Product.SellerId == query.SellerId.Value);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        // Sorting
        var isAsc = string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        if (string.Equals(query.SortBy, "status", StringComparison.OrdinalIgnoreCase))
        {
            dbQuery = isAsc ? dbQuery.OrderBy(r => r.Status).ThenByDescending(r => r.CreatedAt)
                            : dbQuery.OrderByDescending(r => r.Status).ThenByDescending(r => r.CreatedAt);
        }
        else
        {
            dbQuery = isAsc ? dbQuery.OrderBy(r => r.CreatedAt)
                            : dbQuery.OrderByDescending(r => r.CreatedAt);
        }

        var items = await dbQuery
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(r => new ProductReportListItemDto(
                r.Id,
                r.ProductId,
                r.Product != null ? r.Product.Name : string.Empty,
                r.Product != null ? r.Product.SellerId : Guid.Empty,
                r.ReporterId,
                r.Reason.ToString(),
                r.Description,
                DeserializeImages(r.EvidenceImages),
                r.Status.ToString(),
                r.CreatedAt,
                r.ReviewedBy,
                r.ReviewedAt,
                r.ResolutionNote
            ))
            .ToListAsync(cancellationToken);

        return new PagedData<ProductReportListItemDto>(items, PaginationMetadata.Create(safePage, safePageSize, totalCount));
    }

    public async Task<ProductReportDto> ModerateReportAsync(
        Guid reportId,
        ModerateProductReportRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Action))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Action is required. Allowed values: BAN_PRODUCT, DISMISS, INVESTIGATE.");
        }

        if (string.IsNullOrWhiteSpace(request.ResolutionNote))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Resolution note is required.");
        }

        var report = await _dbContext.ProductReports
            .Include(r => r.Product)
                .ThenInclude(p => p!.Variants)
            .FirstOrDefaultAsync(r => r.Id == reportId && !r.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("REPORT_NOT_FOUND", "ProductReport", reportId);

        if (report.Status == ReportStatus.Resolved || report.Status == ReportStatus.Dismissed)
        {
            throw new BusinessRuleValidationException("REPORT_ALREADY_CLOSED", "This report has already been closed and cannot be re-moderated.");
        }

        var action = request.Action.Trim().ToUpperInvariant();
        var now = DateTimeOffset.UtcNow;

        if (action == "BAN_PRODUCT")
        {
            report.Status = ReportStatus.Resolved;
            report.ReviewedBy = adminId;
            report.ReviewedAt = now;
            report.ResolutionNote = request.ResolutionNote.Trim();
            report.UpdatedAt = now;

            if (report.Product != null)
            {
                var oldStatus = report.Product.Status;
                report.Product.Status = ProductStatus.Banned;
                report.Product.StatusReason = null;
                report.Product.UpdatedAt = now;
                report.Product.UpdatedBy = adminId;

                // Deactivate all variants
                foreach (var variant in report.Product.Variants.Where(v => !v.IsDeleted))
                {
                    variant.Status = VariantStatus.Inactive;
                    variant.IsActive = false;
                    variant.UpdatedAt = now;
                    variant.UpdatedBy = adminId;
                }

                // Auto-resolve other active reports for the same product
                var otherReports = await _dbContext.ProductReports
                    .Where(r => r.ProductId == report.ProductId 
                             && r.Id != reportId 
                             && (r.Status == ReportStatus.Pending || r.Status == ReportStatus.Investigating)
                             && !r.IsDeleted)
                    .ToListAsync(cancellationToken);

                foreach (var other in otherReports)
                {
                    other.Status = ReportStatus.Resolved;
                    other.ReviewedBy = adminId;
                    other.ReviewedAt = now;
                    other.ResolutionNote = $"Product permanently banned via report {reportId}: {request.ResolutionNote.Trim()}";
                    other.UpdatedAt = now;
                }

                _dbContext.AddAuditLog(
                    AuditAction.ProductBan,
                    "Product",
                    report.ProductId.ToString(),
                    oldValues: new { Status = oldStatus.ToString() },
                    newValues: new { Status = "Banned", BannedBy = adminId, Note = request.ResolutionNote.Trim(), TriggeredByReportId = reportId },
                    userId: adminId.ToString());
            }

            _dbContext.AddAuditLog(
                AuditAction.ProductReport,
                "ProductReport",
                report.Id.ToString(),
                newValues: new { Status = "Resolved", Action = "BAN_PRODUCT", ResolutionNote = request.ResolutionNote.Trim() },
                userId: adminId.ToString());
        }
        else if (action == "DISMISS")
        {
            report.Status = ReportStatus.Dismissed;
            report.ReviewedBy = adminId;
            report.ReviewedAt = now;
            report.ResolutionNote = request.ResolutionNote.Trim();
            report.UpdatedAt = now;

            _dbContext.AddAuditLog(
                AuditAction.ProductReport,
                "ProductReport",
                report.Id.ToString(),
                newValues: new { Status = "Dismissed", Action = "DISMISS", ResolutionNote = request.ResolutionNote.Trim() },
                userId: adminId.ToString());
        }
        else if (action == "INVESTIGATE")
        {
            report.Status = ReportStatus.Investigating;
            report.ReviewedBy = adminId;
            report.ReviewedAt = now;
            report.ResolutionNote = request.ResolutionNote.Trim();
            report.UpdatedAt = now;

            _dbContext.AddAuditLog(
                AuditAction.ProductReport,
                "ProductReport",
                report.Id.ToString(),
                newValues: new { Status = "Investigating", Action = "INVESTIGATE", ResolutionNote = request.ResolutionNote.Trim() },
                userId: adminId.ToString());
        }
        else
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", $"Unknown action '{request.Action}'. Allowed values: BAN_PRODUCT, DISMISS, INVESTIGATE.");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Deduct 10 reputation points from Seller when Product is banned due to violations (P2-06)
        if (action == "BAN_PRODUCT" && report.Product != null && report.Product.SellerId != Guid.Empty)
        {
            await _userClient.PenalizeSellerAsync(
                report.Product.SellerId,
                penaltyPoints: 10,
                reason: "PRODUCT_BANNED_SELLER",
                referenceId: report.ProductId,
                cancellationToken: cancellationToken);
        }

        // Send In-App notifications based on moderation decision
        var productName = report.Product?.Name ?? "sản phẩm";
        var note = request.ResolutionNote?.Trim() ?? string.Empty;

        if (action == "BAN_PRODUCT")
        {
            // 1. Notify Seller of ban and reputation penalty
            if (report.Product != null && report.Product.SellerId != Guid.Empty)
            {
                await _notificationClient.SendInAppNotificationAsync(
                    report.Product.SellerId,
                    "CẢNH BÁO: Sản phẩm bị cấm do đơn tố cáo vi phạm",
                    $"Sản phẩm '{productName}' đã bị gỡ vĩnh viễn sau khi Quản trị viên xác minh đơn tố cáo vi phạm. Tài khoản Shop bị trừ 10 điểm uy tín. Phán quyết: {note}.",
                    "PRODUCT_BANNED_REPORT",
                    $"{{\"productId\":\"{report.ProductId}\",\"reportId\":\"{report.Id}\",\"penaltyPoints\":10}}",
                    cancellationToken);
            }

            // 2. Notify Reporter (Buyer) that their report was resolved
            if (report.ReporterId != Guid.Empty)
            {
                await _notificationClient.SendInAppNotificationAsync(
                    report.ReporterId,
                    "Đơn tố cáo sản phẩm đã được xử lý thành công",
                    $"Cảm ơn bạn đã hỗ trợ bảo vệ cộng đồng. Đơn tố cáo sản phẩm '{productName}' của bạn đã được Quản trị viên xác minh chính xác và xử lý nghiêm khắc.",
                    "REPORT_RESOLVED",
                    $"{{\"reportId\":\"{report.Id}\",\"productId\":\"{report.ProductId}\",\"resolution\":\"Resolved\"}}",
                    cancellationToken);
            }
        }
        else if (action == "DISMISS")
        {
            // Notify Reporter (Buyer) that report was dismissed
            if (report.ReporterId != Guid.Empty)
            {
                await _notificationClient.SendInAppNotificationAsync(
                    report.ReporterId,
                    "Thông báo kết quả xử lý đơn tố cáo",
                    $"Đơn tố cáo sản phẩm '{productName}' của bạn đã được xem xét và bác bỏ do Người bán đã cung cấp đầy đủ giấy tờ hợp lệ hoặc bằng chứng chưa đủ căn cứ. Ghi chú: {note}.",
                    "REPORT_DISMISSED",
                    $"{{\"reportId\":\"{report.Id}\",\"productId\":\"{report.ProductId}\",\"resolution\":\"Dismissed\"}}",
                    cancellationToken);
            }
        }

        return MapToDto(report);
    }

    private static ProductReportDto MapToDto(ProductReport report)
    {
        return new ProductReportDto(
            report.Id,
            report.ProductId,
            report.ReporterId,
            report.Reason.ToString(),
            report.Description,
            DeserializeImages(report.EvidenceImages),
            report.Status.ToString(),
            report.CreatedAt,
            report.ReviewedBy,
            report.ReviewedAt,
            report.ResolutionNote
        );
    }

    private static List<string>? DeserializeImages(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json);
        }
        catch
        {
            return null;
        }
    }
}
