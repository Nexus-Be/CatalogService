namespace Catalog.Application.Services.Category;

using System;
using System.ComponentModel.DataAnnotations;

public sealed record CreateCategoryRequest(
    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(100, ErrorMessage = "Category name cannot exceed 100 characters.")]
    string Name,

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    string? Description = null,

    Guid? ParentId = null,

    [Range(1, short.MaxValue, ErrorMessage = "Category level must be greater than or equal to 1.")]
    short Level = 1,

    [Range(0, int.MaxValue, ErrorMessage = "Sort order must be greater than or equal to 0.")]
    int SortOrder = 0);

public sealed record UpdateCategoryRequest(
    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(100, ErrorMessage = "Category name cannot exceed 100 characters.")]
    string Name,

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    string? Description = null,

    [Range(1, short.MaxValue, ErrorMessage = "Category level must be greater than or equal to 1.")]
    short Level = 1,

    [Range(0, int.MaxValue, ErrorMessage = "Sort order must be greater than or equal to 0.")]
    int SortOrder = 0,

    bool IsDisabled = false);

public sealed record CategoryListQuery(
    int Page = 1,
    int PageSize = 10,
    string? SortBy = "sortOrder",
    string? SortDirection = "asc",
    Guid? ParentId = null,
    bool Tree = false,
    string? Keyword = null,
    bool IncludeDisabled = false);
