using Atlas.Application.BatchProcess.Models;
using FluentValidation;

namespace Atlas.Application.BatchProcess.Validators;

public sealed class BatchJobUpdateRequestValidator : AbstractValidator<BatchJobUpdateRequest>
{
    public BatchJobUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("任务名称不能为空")
            .MaximumLength(200).WithMessage("任务名称不能超过200个字符");

        RuleFor(x => x.DataSourceType)
            .NotEmpty().WithMessage("数据源类型不能为空");

        RuleFor(x => x.DataSourceConfig)
            .NotEmpty().WithMessage("数据源配置不能为空");

        RuleFor(x => x.ShardConfig)
            .NotEmpty().WithMessage("分片配置不能为空");

        RuleFor(x => x.BatchSize)
            .InclusiveBetween(1, 10000).WithMessage("批次大小必须在 1~10000 之间");

        RuleFor(x => x.MaxConcurrency)
            .InclusiveBetween(1, 64).WithMessage("最大并发数必须在 1~64 之间");

        RuleFor(x => x.TimeoutSeconds)
            .InclusiveBetween(1, 86400).WithMessage("超时时间必须在 1~86400 秒之间");
    }
}
