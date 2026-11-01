namespace Catalog.Application.Services.Category;

using System;
using System.Collections.Generic;

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
