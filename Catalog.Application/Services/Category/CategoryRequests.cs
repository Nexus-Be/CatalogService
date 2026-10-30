namespace Catalog.Application.Services.Category;

using System;

public sealed record CreateCategoryRequest(
    Guid? ParentId,
    string Name,
    string? Description,
    int SortOrder = 0);

public sealed record UpdateCategoryRequest(
    string Name,
    string? Description,
    int SortOrder = 0,
    bool IsDisabled = false);
