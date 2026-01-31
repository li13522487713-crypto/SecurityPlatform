namespace Atlas.Core.Identity;

public interface IProjectContextAccessor
{
    ProjectContext GetCurrent();
}
