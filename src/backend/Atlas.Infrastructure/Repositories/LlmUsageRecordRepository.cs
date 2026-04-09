using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class LlmUsageRecordRepository : RepositoryBase<LlmUsageRecord>
{
    public LlmUsageRecordRepository(ISqlSugarClient db)
        : base(db)
    {
    }
}
