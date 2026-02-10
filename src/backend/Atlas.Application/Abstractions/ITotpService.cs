namespace Atlas.Application.Abstractions;

/// <summary>
/// TOTP (Time-based One-Time Password) service for MFA support per RFC 6238.
/// </summary>
public interface ITotpService
{
    /// <summary>
    /// Generates a new random secret key (Base32 encoded).
    /// </summary>
    string GenerateSecretKey();

    /// <summary>
    /// Generates the current TOTP code for the given secret key.
    /// </summary>
    string GenerateCode(string secretKey, DateTimeOffset? timestamp = null);

    /// <summary>
    /// Validates a TOTP code against the secret key, allowing a window of tolerance.
    /// </summary>
    bool ValidateCode(string secretKey, string code, DateTimeOffset? timestamp = null);

    /// <summary>
    /// Generates the provisioning URI for authenticator apps (otpauth:// format).
    /// </summary>
    string GenerateProvisioningUri(string secretKey, string userIdentifier, string issuer);
}
