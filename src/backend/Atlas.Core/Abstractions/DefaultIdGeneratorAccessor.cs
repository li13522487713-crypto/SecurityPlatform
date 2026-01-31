using Atlas.Core.Identity;

namespace Atlas.Core.Abstractions;

public sealed class DefaultIdGeneratorAccessor : IIdGeneratorAccessor
{
    private readonly IIdGeneratorProvider _idGeneratorProvider;
    private readonly IAppContextAccessor _appContextAccessor;

    public DefaultIdGeneratorAccessor(
        IIdGeneratorProvider idGeneratorProvider,
        IAppContextAccessor appContextAccessor)
    {
        _idGeneratorProvider = idGeneratorProvider;
        _appContextAccessor = appContextAccessor;
    }

    public long NextId()
    {
        var context = _appContextAccessor.GetCurrent();
        return _idGeneratorProvider.NextId(context.TenantId, context.AppId);
    }
}
