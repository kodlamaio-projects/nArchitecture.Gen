using NArchitecture.Core.Application.Dtos;

namespace Application.Features.Brands.Dtos;

public class BrandSummaryDto : IDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal? Discount { get; set; }
    public string? Secret { get; set; }
}
