using System.Text.RegularExpressions;
using Atlas.Application.Options;

namespace Atlas.Application.Security;

public static class PasswordPolicy
{
    public static bool IsCompliant(string password, PasswordPolicyOptions options, out string errorMessage)
    {
        return IsCompliant(password, options, (_, _, fallback) => fallback, out errorMessage);
    }

    public static bool IsCompliant(
        string password,
        PasswordPolicyOptions options,
        Func<string, object[], string, string> messageResolver,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(password))
        {
            errorMessage = messageResolver("PasswordRequired", Array.Empty<object>(), "Password is required.");
            return false;
        }

        if (password.Length < options.MinLength)
        {
            errorMessage = messageResolver(
                "PasswordMinLength",
                [options.MinLength],
                $"Password must be at least {options.MinLength} characters long.");
            return false;
        }

        if (options.RequireUppercase && !Regex.IsMatch(password, "[A-Z]"))
        {
            errorMessage = messageResolver("PasswordRequireUppercase", Array.Empty<object>(), "Password must contain an uppercase letter.");
            return false;
        }

        if (options.RequireLowercase && !Regex.IsMatch(password, "[a-z]"))
        {
            errorMessage = messageResolver("PasswordRequireLowercase", Array.Empty<object>(), "Password must contain a lowercase letter.");
            return false;
        }

        if (options.RequireDigit && !Regex.IsMatch(password, "[0-9]"))
        {
            errorMessage = messageResolver("PasswordRequireDigit", Array.Empty<object>(), "Password must contain a digit.");
            return false;
        }

        if (options.RequireNonAlphanumeric && !Regex.IsMatch(password, "[^a-zA-Z0-9]"))
        {
            errorMessage = messageResolver(
                "PasswordRequireNonAlphanumeric",
                Array.Empty<object>(),
                "Password must contain a non-alphanumeric character.");
            return false;
        }

        return true;
    }
}
