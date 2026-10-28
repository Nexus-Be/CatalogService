namespace Catalog.Application.Services.Category;

using System;

public sealed record CategoryDto(
    Guid Id,
    Guid? ParentId,
    string Name,
    string? Description,
    short Level,
    int SortOrder,
    bool IsDisabled);
public sealed record CategoryItemDto(
    Guid CategoryId,
    Guid? ParentId,
    string Name,
    string? Description,
    short Level,
    int SortOrder,
    List<CategoryItemDto>? Children = null);
public sealed record CategoryListQuery(
    Guid? ParentId = null,
    bool Tree = false,
    string? Keyword = null);    
