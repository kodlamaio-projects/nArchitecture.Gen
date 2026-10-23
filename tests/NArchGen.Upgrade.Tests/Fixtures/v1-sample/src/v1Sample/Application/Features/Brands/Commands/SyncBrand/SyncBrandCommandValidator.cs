using FluentValidation;

namespace Application.Features.Brands.Commands.SyncBrand;

public class SyncBrandCommandValidator : AbstractValidator<SyncBrandCommand>
{
    public SyncBrandCommandValidator() { }
}