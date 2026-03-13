using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class AdminAiConfigUpdateRequestValidator : AbstractValidator<AdminAiConfigUpdateRequest>
{
    public AdminAiConfigUpdateRequestValidator()
    {
        RuleFor(x => x.MaxDailyTokensPerUser).InclusiveBetween(1000, 10_000_000);
        RuleFor(x => x.MaxKnowledgeRetrievalCount).InclusiveBetween(1, 100);
    }
}
