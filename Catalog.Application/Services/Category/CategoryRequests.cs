namespace Catalog.Application.Services.Category;

using System;

public sealed record CreateCategoryRequest(
    string Name,
    string? Description = null,
    Guid? ParentId = null,
    short Level = 1,
    int SortOrder = 0);

public sealed record UpdateCategoryRequest(
    string Name,
    string? Description = null,
    short Level = 1,
    int SortOrder = 0,
    bool IsDisabled = false);

public sealed record CategoryListQuery(
    Guid? ParentId = null,
    bool Tree = false,
    string? Keyword = null);
