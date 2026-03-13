using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class AiPluginCreateRequestValidator : AbstractValidator<AiPluginCreateRequest>
{
    public AiPluginCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => x.Description is not null);
        RuleFor(x => x.Icon).MaximumLength(256).When(x => x.Icon is not null);
        RuleFor(x => x.Category).MaximumLength(64).When(x => x.Category is not null);
        RuleFor(x => x.DefinitionJson).MaximumLength(500000).When(x => x.DefinitionJson is not null);
    }
}

public sealed class AiPluginUpdateRequestValidator : AbstractValidator<AiPluginUpdateRequest>
{
    public AiPluginUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => x.Description is not null);
        RuleFor(x => x.Icon).MaximumLength(256).When(x => x.Icon is not null);
        RuleFor(x => x.Category).MaximumLength(64).When(x => x.Category is not null);
        RuleFor(x => x.DefinitionJson).MaximumLength(500000).When(x => x.DefinitionJson is not null);
    }
}

public sealed class AiPluginDebugRequestValidator : AbstractValidator<AiPluginDebugRequest>
{
    public AiPluginDebugRequestValidator()
    {
        RuleFor(x => x.ApiId).GreaterThan(0).When(x => x.ApiId.HasValue);
        RuleFor(x => x.InputJson).MaximumLength(500000).When(x => x.InputJson is not null);
    }
}

public sealed class AiPluginOpenApiImportRequestValidator : AbstractValidator<AiPluginOpenApiImportRequest>
{
    public AiPluginOpenApiImportRequestValidator()
    {
        RuleFor(x => x.OpenApiJson).NotEmpty().MaximumLength(2000000);
    }
}

public sealed class AiPluginApiCreateRequestValidator : AbstractValidator<AiPluginApiCreateRequest>
{
    public AiPluginApiCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => x.Description is not null);
        RuleFor(x => x.Method).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Path).NotEmpty().MaximumLength(512);
        RuleFor(x => x.RequestSchemaJson).MaximumLength(500000).When(x => x.RequestSchemaJson is not null);
        RuleFor(x => x.ResponseSchemaJson).MaximumLength(500000).When(x => x.ResponseSchemaJson is not null);
        RuleFor(x => x.TimeoutSeconds).InclusiveBetween(1, 600);
    }
}

public sealed class AiPluginApiUpdateRequestValidator : AbstractValidator<AiPluginApiUpdateRequest>
{
    public AiPluginApiUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => x.Description is not null);
        RuleFor(x => x.Method).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Path).NotEmpty().MaximumLength(512);
        RuleFor(x => x.RequestSchemaJson).MaximumLength(500000).When(x => x.RequestSchemaJson is not null);
        RuleFor(x => x.ResponseSchemaJson).MaximumLength(500000).When(x => x.ResponseSchemaJson is not null);
        RuleFor(x => x.TimeoutSeconds).InclusiveBetween(1, 600);
    }
}
