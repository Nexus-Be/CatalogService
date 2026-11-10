namespace Catalog.Domain.Entities;

using System;
using Catalog.Domain.Base;
using Catalog.Domain.Enums;

public class ProductReport : BaseEntity<Guid>
{
    public required Guid ProductId { get; set; }
    public required Guid ReporterId { get; set; }
    public required ReportReason Reason { get; set; }
    public required string Description { get; set; }
    public string? EvidenceImages { get; set; } // JSON string array of URLs stored as jsonb
    public required ReportStatus Status { get; set; } = ReportStatus.Pending;
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ResolutionNote { get; set; }

    // Navigation
    public Product? Product { get; set; }
}
