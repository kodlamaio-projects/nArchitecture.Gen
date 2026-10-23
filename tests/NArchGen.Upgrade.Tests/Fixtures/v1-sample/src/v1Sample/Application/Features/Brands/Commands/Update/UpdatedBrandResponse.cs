using NArchitecture.Core.Application.Responses;

namespace Application.Features.Brands.Commands.Update;

public class UpdatedBrandResponse : IResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public decimal? Discount { get; set; }
}