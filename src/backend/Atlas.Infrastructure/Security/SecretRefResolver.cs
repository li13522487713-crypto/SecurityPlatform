using Atlas.Core.Exceptions;
using Atlas.Core.Models;

namespace Atlas.Infrastructure.Security;

public sealed class SecretRefResolver : ISecretRefResolver
{
    private const string SecretRefPrefix = "secretref:";

    public string Resolve(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        if (!normalized.StartsWith(SecretRefPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        var payload = normalized[SecretRefPrefix.Length..];
        if (payload.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
        {
            var envName = payload[4..].Trim();
            if (string.IsNullOrWhiteSpace(envName))
            {
                throw new BusinessException("SecretRef 环境变量名称不能为空。", ErrorCodes.ValidationError);
            }

            var envValue = Environment.GetEnvironmentVariable(envName);
            if (string.IsNullOrWhiteSpace(envValue))
            {
                throw new BusinessException($"SecretRef 环境变量未设置: {envName}", ErrorCodes.NotFound);
            }

            return envValue;
        }

        if (payload.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            var filePath = payload[5..].Trim();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new BusinessException("SecretRef 文件路径不能为空。", ErrorCodes.ValidationError);
            }

            if (!File.Exists(filePath))
            {
                throw new BusinessException($"SecretRef 文件不存在: {filePath}", ErrorCodes.NotFound);
            }

            return File.ReadAllText(filePath).Trim();
        }

        throw new BusinessException($"不支持的 SecretRef 协议: {normalized}", ErrorCodes.ValidationError);
    }
}
