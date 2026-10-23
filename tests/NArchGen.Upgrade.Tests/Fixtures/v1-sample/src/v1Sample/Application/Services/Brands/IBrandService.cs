using NArchitecture.Core.Persistence.Paging;
using Domain.Entities;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Application.Services.Brands;

public interface IBrandService
{
    Task<Brand?> GetAsync(
        Expression<Func<Brand, bool>> predicate,
        Func<IQueryable<Brand>, IIncludableQueryable<Brand, object>>? include = null,
        bool withDeleted = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default
    );
    Task<IPaginate<Brand>?> GetListAsync(
        Expression<Func<Brand, bool>>? predicate = null,
        Func<IQueryable<Brand>, IOrderedQueryable<Brand>>? orderBy = null,
        Func<IQueryable<Brand>, IIncludableQueryable<Brand, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withDeleted = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default
    );
    Task<Brand> AddAsync(Brand brand);
    Task<Brand> UpdateAsync(Brand brand);
    Task<Brand> DeleteAsync(Brand brand, bool permanent = false);
}
