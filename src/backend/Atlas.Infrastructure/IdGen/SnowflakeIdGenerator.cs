using Atlas.Core.Abstractions;
using IdGen;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.IdGen;

public sealed class SnowflakeIdGenerator : IIdGenerator
{
    private readonly IdGenerator _generator;

    public SnowflakeIdGenerator(IOptions<SnowflakeOptions> options)
    {
        var structure = new IdStructure(41, 10, 12);
        var config = new IdGeneratorOptions(structure);
        _generator = new IdGenerator(options.Value.GeneratorId, config);
    }

    public SnowflakeIdGenerator(int generatorId, IdGeneratorOptions options)
    {
        _generator = new IdGenerator(generatorId, options);
    }

    public long NextId() => _generator.CreateId();
}
