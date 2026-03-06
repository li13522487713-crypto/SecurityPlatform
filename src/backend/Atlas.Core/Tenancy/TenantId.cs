namespace Atlas.Core.Tenancy;

public readonly record struct TenantId(Guid Value)
{
    public static TenantId Empty => new(Guid.Empty);
    public bool IsEmpty => Value == Guid.Empty;
    public override string ToString() => Value.ToString("D");

    public static implicit operator Guid(TenantId tenantId) => tenantId.Value;
    public static implicit operator TenantId(Guid value) => new(value);

    public static bool operator ==(TenantId left, Guid right) => left.Value == right;
    public static bool operator !=(TenantId left, Guid right) => left.Value != right;
    public static bool operator ==(Guid left, TenantId right) => left == right.Value;
    public static bool operator !=(Guid left, TenantId right) => left != right.Value;
}