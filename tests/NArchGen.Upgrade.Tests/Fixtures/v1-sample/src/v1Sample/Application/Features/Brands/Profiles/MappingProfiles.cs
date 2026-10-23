using Application.Features.Brands.Commands.Create;
using Application.Features.Brands.Commands.Delete;
using Application.Features.Brands.Commands.Update;
using Application.Features.Brands.Queries.GetById;
using Application.Features.Brands.Queries.GetList;
using AutoMapper;
using NArchitecture.Core.Application.Responses;
using Domain.Entities;
using NArchitecture.Core.Persistence.Paging;

namespace Application.Features.Brands.Profiles;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<CreateBrandCommand, Brand>();
        CreateMap<Brand, CreatedBrandResponse>();

        CreateMap<UpdateBrandCommand, Brand>();
        CreateMap<Brand, UpdatedBrandResponse>();

        CreateMap<DeleteBrandCommand, Brand>();
        CreateMap<Brand, DeletedBrandResponse>();

        CreateMap<Brand, GetByIdBrandResponse>();

        CreateMap<Brand, GetListBrandListItemDto>();
        CreateMap<IPaginate<Brand>, GetListResponse<GetListBrandListItemDto>>();
    }
}