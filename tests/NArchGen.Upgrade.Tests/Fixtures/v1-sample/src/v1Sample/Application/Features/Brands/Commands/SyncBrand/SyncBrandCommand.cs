using Application.Features.Brands.Constants;
using Application.Features.Brands.Rules;
using AutoMapper;
using NArchitecture.Core.Application.Pipelines.Authorization;
using NArchitecture.Core.Application.Pipelines.Caching;
using NArchitecture.Core.Application.Pipelines.Logging;
using NArchitecture.Core.Application.Pipelines.Transaction;
using MediatR;
using static Application.Features.Brands.Constants.BrandsOperationClaims;

namespace Application.Features.Brands.Commands.SyncBrand;

public class SyncBrandCommand : IRequest<SyncBrandResponse>, ISecuredRequest, ICacheRemoverRequest, ILoggableRequest, ITransactionalRequest
{

    public string[] Roles => [Admin, Write, BrandsOperationClaims.SyncBrand];

    public bool BypassCache { get; }
    public string? CacheKey { get; }
    public string[]? CacheGroupKey => ["GetBrands"];
    
    public class SyncBrandCommandHandler : IRequestHandler<SyncBrandCommand, SyncBrandResponse>
    {
        private readonly IMapper _mapper;
        private readonly BrandBusinessRules _brandBusinessRules;

        public SyncBrandCommandHandler(IMapper mapper, BrandBusinessRules brandBusinessRules)
        {
            _mapper = mapper;
            _brandBusinessRules = brandBusinessRules;
        }

        public async Task<SyncBrandResponse> Handle(SyncBrandCommand request, CancellationToken cancellationToken)
        {
            SyncBrandResponse response = _mapper.Map<SyncBrandResponse>(null);
            return response;
        }
    }
}
