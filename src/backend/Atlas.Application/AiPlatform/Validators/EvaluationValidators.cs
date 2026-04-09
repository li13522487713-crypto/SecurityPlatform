using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class EvaluationDatasetCreateRequestValidator : AbstractValidator<EvaluationDatasetCreateRequest>
{
    public EvaluationDatasetCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.Scene).MaximumLength(128).When(x => !string.IsNullOrWhiteSpace(x.Scene));
    }
}

public sealed class EvaluationCaseCreateRequestValidator : AbstractValidator<EvaluationCaseCreateRequest>
{
    public EvaluationCaseCreateRequestValidator()
    {
        RuleFor(x => x.Input).NotEmpty().MaximumLength(32000);
        RuleFor(x => x.ExpectedOutput).MaximumLength(32000).When(x => !string.IsNullOrWhiteSpace(x.ExpectedOutput));
        RuleFor(x => x.ReferenceOutput).MaximumLength(32000).When(x => !string.IsNullOrWhiteSpace(x.ReferenceOutput));
        RuleForEach(x => x.Tags!).MaximumLength(64).When(x => x.Tags is not null);
        RuleForEach(x => x.GroundTruthChunkIds!).GreaterThan(0).When(x => x.GroundTruthChunkIds is not null);
        RuleForEach(x => x.GroundTruthCitations!).MaximumLength(64).When(x => x.GroundTruthCitations is not null);
    }
}

public sealed class EvaluationTaskCreateRequestValidator : AbstractValidator<EvaluationTaskCreateRequest>
{
    public EvaluationTaskCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.DatasetId).GreaterThan(0);
        RuleFor(x => x.AgentId).GreaterThan(0);
    }
}
