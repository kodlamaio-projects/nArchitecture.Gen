using FluentValidation;

namespace Application.Features.Brands.Queries.GetBrandByName;

public class GetBrandByNameQueryValidator : AbstractValidator<GetBrandByNameQuery>
{
    public GetBrandByNameQueryValidator() { }
}