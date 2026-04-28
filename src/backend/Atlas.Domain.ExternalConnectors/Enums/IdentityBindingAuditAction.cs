namespace Atlas.Domain.ExternalConnectors.Enums;

public enum IdentityBindingAuditAction
{
    Created = 1,
    Updated = 2,
    Confirmed = 3,
    Revoked = 4,
    ConflictDetected = 5,
    ConflictResolved = 6,
    AutoRebind = 7,
}
