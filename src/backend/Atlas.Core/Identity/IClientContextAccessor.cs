namespace Atlas.Core.Identity;

public interface IClientContextAccessor
{
    ClientContext GetCurrent();
}
