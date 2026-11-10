namespace Catalog.Application.Services.Report;

using System;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Application.Common.Models;

public interface IProductReportService
{
    Task<ProductReportDto> SubmitProductReportAsync(
        Guid productId,
        CreateProductReportRequest request,
        Guid reporterId,
        CancellationToken cancellationToken = default);

    Task<PagedData<ProductReportListItemDto>> GetAdminReportsAsync(
        AdminReportSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ProductReportDto> ModerateReportAsync(
        Guid reportId,
        ModerateProductReportRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);
}
