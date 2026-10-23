using Application.Features.Brands.Rules;
using Application.Services.Repositories;
using NArchitecture.Core.Persistence.Paging;
using Domain.Entities;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Application.Services.Brands;

public class BrandManager : IBrandService
{
    private readonly IBrandRepository _brandRepository;
    private readonly BrandBusinessRules _brandBusinessRules;

    public BrandManager(IBrandRepository brandRepository, BrandBusinessRules brandBusinessRules)
    {
        _brandRepository = brandRepository;
        _brandBusinessRules = brandBusinessRules;
    }

    public async Task<Brand?> GetAsync(
        Expression<Func<Brand, bool>> predicate,
        Func<IQueryable<Brand>, IIncludableQueryable<Brand, object>>? include = null,
        bool withDeleted = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default
    )
    {
        Brand? brand = await _brandRepository.GetAsync(predicate, include, withDeleted, enableTracking, cancellationToken);
        return brand;
    }

    public async Task<IPaginate<Brand>?> GetListAsync(
        Expression<Func<Brand, bool>>? predicate = null,
        Func<IQueryable<Brand>, IOrderedQueryable<Brand>>? orderBy = null,
        Func<IQueryable<Brand>, IIncludableQueryable<Brand, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withDeleted = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default
    )
    {
        IPaginate<Brand> brandList = await _brandRepository.GetListAsync(
            predicate,
            orderBy,
            include,
            index,
            size,
            withDeleted,
            enableTracking,
            cancellationToken
        );
        return brandList;
    }

    public async Task<Brand> AddAsync(Brand brand)
    {
        Brand addedBrand = await _brandRepository.AddAsync(brand);

        return addedBrand;
    }

    public async Task<Brand> UpdateAsync(Brand brand)
    {
        Brand updatedBrand = await _brandRepository.UpdateAsync(brand);

        return updatedBrand;
    }

    public async Task<Brand> DeleteAsync(Brand brand, bool permanent = false)
    {
        Brand deletedBrand = await _brandRepository.DeleteAsync(brand);

        return deletedBrand;
    }
}
