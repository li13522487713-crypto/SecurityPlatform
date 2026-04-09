using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AgenticRagRunHistoryRepository : RepositoryBase<AgenticRagRunHistory>
{
    public AgenticRagRunHistoryRepository(ISqlSugarClient db)
        : base(db)
    {
    }
}
