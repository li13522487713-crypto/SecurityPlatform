using FluentValidation;
using Atlas.Application.Assets.Models;

namespace Atlas.Application.Assets.Validators;

public sealed class AssetCreateRequestValidator : AbstractValidator<AssetCreateRequest>
{
    public AssetCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}