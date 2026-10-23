using Application.Features.Brands.Constants;
using Application.Features.Brands.Rules;
using AutoMapper;
using NArchitecture.Core.Application.Pipelines.Authorization;
using NArchitecture.Core.Application.Pipelines.Caching;
using NArchitecture.Core.Application.Pipelines.Logging;
using MediatR;
using static Application.Features.Brands.Constants.BrandsOperationClaims;

namespace Application.Features.Brands.Queries.GetBrandByName;

public class GetBrandByNameQuery : IRequest<GetBrandByNameResponse>, ISecuredRequest, ICachableRequest, ILoggableRequest
{

    public string[] Roles => [Admin, Read, BrandsOperationClaims.GetBrandByName];

    public bool BypassCache { get; }
    public string? CacheKey => $"GetBrandByName";
    public string? CacheGroupKey => "GetBrands";
    public TimeSpan? SlidingExpiration { get; }
    
    public class GetBrandByNameQueryHandler : IRequestHandler<GetBrandByNameQuery, GetBrandByNameResponse>
    {
        private readonly IMapper _mapper;
        private readonly BrandBusinessRules _brandBusinessRules;

        public GetBrandByNameQueryHandler(IMapper mapper, BrandBusinessRules brandBusinessRules)
        {
            _mapper = mapper;
            _brandBusinessRules = brandBusinessRules;
        }

        public async Task<GetBrandByNameResponse> Handle(GetBrandByNameQuery request, CancellationToken cancellationToken)
        {
            GetBrandByNameResponse response = _mapper.Map<GetBrandByNameResponse>(null);
            return response;
        }
    }
}
