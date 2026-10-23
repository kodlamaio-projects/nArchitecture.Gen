using Application.Features.Brands.Dtos;
using AutoMapper;
using Domain.Entities;

namespace Application.Features.Brands.Profiles;

/// <summary>Hand-written, configuration-heavy profile: member renames, ignores and an expression that needs a human.</summary>
public class BrandReportProfile : Profile
{
    public BrandReportProfile()
    {
        CreateMap<Brand, BrandSummaryDto>()
            .ForMember(d => d.DisplayName, opt => opt.MapFrom(s => s.Name))
            .ForMember(d => d.Secret, opt => opt.Ignore())
            .ForMember(d => d.Label, opt => opt.MapFrom(s => s.Name + " (" + s.Discount + ")"));
    }
}
