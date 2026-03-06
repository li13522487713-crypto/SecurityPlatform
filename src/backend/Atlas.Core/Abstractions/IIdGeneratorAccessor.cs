namespace Atlas.Core.Abstractions;

public interface IIdGeneratorAccessor
{
    long NextId();

    /// <summary>
    /// 兼容旧调用方式：_idGen.Generator.NextId()
    /// </summary>
    IIdGeneratorAccessor Generator => this;
}
