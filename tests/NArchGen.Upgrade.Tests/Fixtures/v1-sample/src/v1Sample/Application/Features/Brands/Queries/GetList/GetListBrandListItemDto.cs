using NArchitecture.Core.Application.Dtos;

namespace Application.Features.Brands.Queries.GetList;

public class GetListBrandListItemDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public decimal? Discount { get; set; }
}