using NArchitecture.Core.Application.Responses;

namespace Application.Features.Brands.Commands.Create;

public class CreatedBrandResponse : IResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public decimal? Discount { get; set; }
}