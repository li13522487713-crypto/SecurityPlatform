using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;

namespace Atlas.Application.Microflows.Services;

internal static class MicroflowSchemaJsonHelper
{
    public static JsonElement ParseRequired(string schemaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowSchemaInvalid, "微流 Schema JSON 无法解析。", 400, innerException: ex);
        }
    }

    public static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
