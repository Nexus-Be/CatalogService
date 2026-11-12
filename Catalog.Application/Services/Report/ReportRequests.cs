namespace Catalog.Application.Services.Report;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Catalog.Application.Common.Models;

public sealed record CreateProductReportRequest(
    [Required(ErrorMessage = "Report reason is required.")]
    string Reason,

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 2000 characters.")]
    string Description
);

public sealed record ModerateProductReportRequest(
    [Required(ErrorMessage = "Action is required.")]
    string Action,

    [Required(ErrorMessage = "Resolution note is required.")]
    [StringLength(2000, MinimumLength = 5, ErrorMessage = "Resolution note must be between 5 and 2000 characters.")]
    string ResolutionNote
);

public sealed record AdminReportSearchQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? Reason = null,
    Guid? ProductId = null,
    Guid? SellerId = null,
    string? SortBy = "createdAt",
    string? SortDirection = "desc"
);
