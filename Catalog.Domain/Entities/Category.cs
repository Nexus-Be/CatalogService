using Catalog.Domain.Base;

namespace Catalog.Domain.Entities;

public class Category : AuditableEntity<Guid>
{
    public Guid? ParentId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public short Level { get; set; }
    public int SortOrder { get; set; }
    public bool IsDisabled { get; set; }

    // Navigation
    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
