namespace Catalog.Application.Services.Report;

using System;
using System.Collections.Generic;

public sealed record ProductReportDto(
    Guid Id,
    Guid ProductId,
    Guid ReporterId,
    string Reason,
    string Description,
    List<string>? EvidenceImages,
    string Status,
    DateTimeOffset CreatedAt,
    Guid? ReviewedBy = null,
    DateTimeOffset? ReviewedAt = null,
    string? ResolutionNote = null
);

public sealed record ProductReportListItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid SellerId,
    Guid ReporterId,
    string Reason,
    string Description,
    List<string>? EvidenceImages,
    string Status,
    DateTimeOffset CreatedAt,
    Guid? ReviewedBy = null,
    DateTimeOffset? ReviewedAt = null,
    string? ResolutionNote = null
);
