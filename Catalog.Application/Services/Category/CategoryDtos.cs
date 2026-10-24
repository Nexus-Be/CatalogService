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
