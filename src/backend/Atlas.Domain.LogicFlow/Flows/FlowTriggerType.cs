namespace Atlas.Domain.LogicFlow.Flows;

public enum FlowTriggerType
{
    Manual = 0,
    Scheduled = 1,
    EventDriven = 2,
    ApiCall = 3,
    DataChange = 4
}
