namespace Atlas.Domain.ExternalConnectors.Enums;

public enum CallbackInboxStatus
{
    Received = 1,
    Verified = 2,
    Processed = 3,
    Failed = 4,
    Duplicated = 5,
    DeadLetter = 6,
}

public enum CallbackInboxKind
{
    ApprovalStatus = 1,
    ContactChange = 2,
    MessageInteraction = 3,
    Other = 99,
}
