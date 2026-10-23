using NArchitecture.Core.Application.Responses;

namespace Application.Features.Brands.Queries.GetById;

public class GetByIdBrandResponse : IResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public decimal? Discount { get; set; }
}