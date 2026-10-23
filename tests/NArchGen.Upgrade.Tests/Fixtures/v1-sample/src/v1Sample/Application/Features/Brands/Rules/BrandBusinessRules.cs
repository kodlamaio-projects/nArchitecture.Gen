using Application.Features.Brands.Constants;
using Application.Services.Repositories;
using NArchitecture.Core.Application.Rules;
using NArchitecture.Core.CrossCuttingConcerns.Exception.Types;
using NArchitecture.Core.Localization.Abstraction;
using Domain.Entities;

namespace Application.Features.Brands.Rules;

public class BrandBusinessRules : BaseBusinessRules
{
    private readonly IBrandRepository _brandRepository;
    private readonly ILocalizationService _localizationService;

    public BrandBusinessRules(IBrandRepository brandRepository, ILocalizationService localizationService)
    {
        _brandRepository = brandRepository;
        _localizationService = localizationService;
    }

    private async Task throwBusinessException(string messageKey)
    {
        string message = await _localizationService.GetLocalizedAsync(messageKey, BrandsBusinessMessages.SectionName);
        throw new BusinessException(message);
    }

    public async Task BrandShouldExistWhenSelected(Brand? brand)
    {
        if (brand == null)
            await throwBusinessException(BrandsBusinessMessages.BrandNotExists);
    }

    public async Task BrandIdShouldExistWhenSelected(Guid id, CancellationToken cancellationToken)
    {
        Brand? brand = await _brandRepository.GetAsync(
            predicate: b => b.Id == id,
            enableTracking: false,
            cancellationToken: cancellationToken
        );
        await BrandShouldExistWhenSelected(brand);
    }
}