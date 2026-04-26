using GloryCafe.Domain.Common;

namespace GloryCafe.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = null!;
    public int DisplayOrder { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
