using FluentValidation;
using Atlas.Domain.Assets.Entities;

namespace Atlas.Application.Assets.Validators;

public sealed class AssetValidator : AbstractValidator<Asset>
{
    public AssetValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}