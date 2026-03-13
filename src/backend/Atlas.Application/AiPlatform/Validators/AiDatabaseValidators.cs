using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class AiDatabaseCreateRequestValidator : AbstractValidator<AiDatabaseCreateRequest>
{
    public AiDatabaseCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.TableSchema).NotEmpty().MaximumLength(200000);
        RuleFor(x => x.BotId).GreaterThan(0).When(x => x.BotId.HasValue);
    }
}

public sealed class AiDatabaseUpdateRequestValidator : AbstractValidator<AiDatabaseUpdateRequest>
{
    public AiDatabaseUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.TableSchema).NotEmpty().MaximumLength(200000);
        RuleFor(x => x.BotId).GreaterThan(0).When(x => x.BotId.HasValue);
    }
}

public sealed class AiDatabaseRecordCreateRequestValidator : AbstractValidator<AiDatabaseRecordCreateRequest>
{
    public AiDatabaseRecordCreateRequestValidator()
    {
        RuleFor(x => x.DataJson).NotEmpty().MaximumLength(500000);
    }
}

public sealed class AiDatabaseRecordUpdateRequestValidator : AbstractValidator<AiDatabaseRecordUpdateRequest>
{
    public AiDatabaseRecordUpdateRequestValidator()
    {
        RuleFor(x => x.DataJson).NotEmpty().MaximumLength(500000);
    }
}

public sealed class AiDatabaseSchemaValidateRequestValidator : AbstractValidator<AiDatabaseSchemaValidateRequest>
{
    public AiDatabaseSchemaValidateRequestValidator()
    {
        RuleFor(x => x.TableSchema).NotEmpty().MaximumLength(200000);
    }
}

public sealed class AiDatabaseImportRequestValidator : AbstractValidator<AiDatabaseImportRequest>
{
    public AiDatabaseImportRequestValidator()
    {
        RuleFor(x => x.FileId).GreaterThan(0);
    }
}
