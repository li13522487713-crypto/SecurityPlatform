using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class AiDatabaseCreateRequestValidator : AbstractValidator<AiDatabaseCreateRequest>
{
    public AiDatabaseCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.BotId).GreaterThan(0).When(x => x.BotId.HasValue);
        RuleFor(x => x.WorkspaceId).GreaterThan(0).When(x => x.WorkspaceId.HasValue);
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.TableSchema) || x.Fields is { Count: > 0 })
            .WithMessage("TableSchema 与 Fields 至少提供一项。");
        RuleFor(x => x.TableSchema).MaximumLength(200000).When(x => !string.IsNullOrWhiteSpace(x.TableSchema));
        RuleForEach(x => x.Fields!).SetValidator(new AiDatabaseFieldItemValidator()).When(x => x.Fields is { Count: > 0 });
    }
}

public sealed class AiDatabaseUpdateRequestValidator : AbstractValidator<AiDatabaseUpdateRequest>
{
    public AiDatabaseUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.BotId).GreaterThan(0).When(x => x.BotId.HasValue);
        RuleFor(x => x.WorkspaceId).GreaterThan(0).When(x => x.WorkspaceId.HasValue);
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.TableSchema) || x.Fields is { Count: > 0 })
            .WithMessage("TableSchema 与 Fields 至少提供一项。");
        RuleFor(x => x.TableSchema).MaximumLength(200000).When(x => !string.IsNullOrWhiteSpace(x.TableSchema));
        RuleForEach(x => x.Fields!).SetValidator(new AiDatabaseFieldItemValidator()).When(x => x.Fields is { Count: > 0 });
    }
}

public sealed class AiDatabaseFieldItemValidator : AbstractValidator<AiDatabaseFieldItem>
{
    public AiDatabaseFieldItemValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Description).MaximumLength(512).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.Type).NotEmpty().MaximumLength(32);
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

/// <summary>D5：批量插入请求校验。Rows 不能为空且单行 JSON 长度受限。</summary>
public sealed class AiDatabaseRecordBulkCreateRequestValidator : AbstractValidator<AiDatabaseRecordBulkCreateRequest>
{
    public AiDatabaseRecordBulkCreateRequestValidator()
    {
        RuleFor(x => x.Rows).NotNull().NotEmpty().WithMessage("批量插入行数不能为空。");
        RuleForEach(x => x.Rows).NotEmpty().MaximumLength(500000);
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

public sealed class AiDatabaseModeUpdateRequestValidator : AbstractValidator<AiDatabaseModeUpdateRequest>
{
    public AiDatabaseModeUpdateRequestValidator()
    {
        RuleFor(x => x.QueryMode).IsInEnum();
        RuleFor(x => x.ChannelScope).IsInEnum();
    }
}

public sealed class AiDatabaseChannelConfigsUpdateRequestValidator : AbstractValidator<AiDatabaseChannelConfigsUpdateRequest>
{
    public AiDatabaseChannelConfigsUpdateRequestValidator()
    {
        RuleFor(x => x.Items).NotNull().NotEmpty();
        RuleForEach(x => x.Items).ChildRules(child =>
        {
            child.RuleFor(x => x.ChannelKey).NotEmpty().MaximumLength(64);
            child.RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(64);
            child.RuleFor(x => x.PublishChannelType).MaximumLength(32).When(x => !string.IsNullOrWhiteSpace(x.PublishChannelType));
            child.RuleFor(x => x.CredentialKind).MaximumLength(32).When(x => !string.IsNullOrWhiteSpace(x.CredentialKind));
        });
    }
}
