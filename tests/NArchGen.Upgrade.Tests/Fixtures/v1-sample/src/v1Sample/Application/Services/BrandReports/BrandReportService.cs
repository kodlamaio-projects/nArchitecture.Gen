using Application.Features.Brands.Dtos;
using Application.Services.Repositories;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Entities;

namespace Application.Services.BrandReports;

/// <summary>A service (not a handler) that leans on IMapper: single, collection and queryable projections.</summary>
public class BrandReportService
{
    private readonly IBrandRepository _brandRepository;
    private readonly IMapper _mapper;

    public BrandReportService(IBrandRepository brandRepository, IMapper mapper)
    {
        _brandRepository = brandRepository;
        _mapper = mapper;
    }

    public BrandSummaryDto Summarize(Brand brand)
    {
        return _mapper.Map<BrandSummaryDto>(brand);
    }

    public List<BrandSummaryDto> Summarize(IList<Brand> brands)
    {
        var summaries = _mapper.Map<List<BrandSummaryDto>>(brands);
        return summaries;
    }

    public async Task<BrandSummaryDto?> SummarizeAsync(Guid id, CancellationToken cancellationToken)
    {
        var brand = await _brandRepository.GetAsync(predicate: b => b.Id == id, cancellationToken: cancellationToken);
        return brand is null ? null : _mapper.Map<BrandSummaryDto>(brand);
    }

    public IQueryable<BrandSummaryDto> Query()
    {
        return _brandRepository.Query().ProjectTo<BrandSummaryDto>(_mapper.ConfigurationProvider);
    }
}
