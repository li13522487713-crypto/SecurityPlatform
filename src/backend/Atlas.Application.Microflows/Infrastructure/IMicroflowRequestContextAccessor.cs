using Atlas.Application.Microflows.Contracts;

namespace Atlas.Application.Microflows.Infrastructure;

public interface IMicroflowRequestContextAccessor
{
    MicroflowRequestContext Current { get; }
}
