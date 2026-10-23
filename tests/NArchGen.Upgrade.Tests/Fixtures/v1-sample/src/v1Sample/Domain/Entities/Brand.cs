using NArchitecture.Core.Persistence.Repositories;

namespace Domain.Entities;

public class Brand : Entity<Guid>
{
    public string Name { get; set; }
    public decimal? Discount { get; set; }

    public Brand()
    {
        Name = string.Empty;
    }

    public Brand(Guid id, string name, decimal? discount)
        : base(id)
    {
        Name = name;
        Discount = discount;
    }
}
