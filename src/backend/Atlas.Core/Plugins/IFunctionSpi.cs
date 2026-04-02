namespace Atlas.Core.Plugins;

public interface IFunctionSpi
{
    string Name { get; }
    string Category { get; }
    object Invoke(object[] args);
}
