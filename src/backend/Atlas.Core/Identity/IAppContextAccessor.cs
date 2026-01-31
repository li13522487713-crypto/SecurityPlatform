namespace Atlas.Core.Identity;

public interface IAppContextAccessor
{
    IAppContext GetCurrent();
    string GetAppId();
    IDisposable BeginScope(IAppContext context);
}
