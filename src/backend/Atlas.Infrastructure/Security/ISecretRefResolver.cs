namespace Atlas.Infrastructure.Security;

public interface ISecretRefResolver
{
    string Resolve(string? value);
}
