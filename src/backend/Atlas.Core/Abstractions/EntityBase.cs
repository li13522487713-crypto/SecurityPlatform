namespace Atlas.Core.Abstractions;

public abstract class EntityBase
{
    public long Id { get; protected set; }

    /// <summary>
    /// 允许子类（或创建流程的基础设施代码）在特殊场景下覆盖实体 ID，例如从外部系统导入时。
    /// </summary>
    protected void SetId(long id) => Id = id;
}
