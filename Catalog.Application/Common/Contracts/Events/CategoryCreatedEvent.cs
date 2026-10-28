namespace Catalog.Application.Common.Contracts.Events;

using System;

/// <summary>
/// Event phát hành khi một danh mục sản phẩm mới được tạo thành công.
/// Tuân thủ quy chuẩn đặc tả tại specs/03_EVENT_MESSAGING.md.
/// </summary>
public sealed record CategoryCreatedEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid CategoryId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? ParentId { get; init; }
    public short Level { get; init; }
    public int SortOrder { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

