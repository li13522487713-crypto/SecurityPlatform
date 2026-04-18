using Atlas.Core.Exceptions;
using Atlas.Infrastructure.Services.AiPlatform;

namespace Atlas.SecurityPlatform.Tests.Services.AiPlatform;

public sealed class AiDatabaseValueCoercerTests
{
    private const string SimpleSchema = """
        [
          { "name": "orderId", "type": "string", "required": true },
          { "name": "amount", "type": "number" },
          { "name": "qty", "type": "integer" },
          { "name": "active", "type": "boolean" },
          { "name": "createdAt", "type": "date" },
          { "name": "tags", "type": "array" },
          { "name": "extra", "type": "json" }
        ]
        """;

    [Fact]
    public void ParseColumns_ShouldYieldExpectedFieldTypes()
    {
        var columns = AiDatabaseValueCoercer.ParseColumns(SimpleSchema);
        Assert.Equal(7, columns.Count);
        Assert.Equal(AiDatabaseFieldType.String, columns[0].Type);
        Assert.True(columns[0].Required);
        Assert.Equal(AiDatabaseFieldType.Number, columns[1].Type);
        Assert.Equal(AiDatabaseFieldType.Integer, columns[2].Type);
        Assert.Equal(AiDatabaseFieldType.Boolean, columns[3].Type);
        Assert.Equal(AiDatabaseFieldType.Date, columns[4].Type);
        Assert.Equal(AiDatabaseFieldType.Array, columns[5].Type);
        Assert.Equal(AiDatabaseFieldType.Json, columns[6].Type);
    }

    [Fact]
    public void ParseColumns_OnInvalidJson_ShouldReturnEmpty()
    {
        var columns = AiDatabaseValueCoercer.ParseColumns("not-json");
        Assert.Empty(columns);
    }

    [Fact]
    public void Coerce_ShouldNormalizeStringNumberAndBoolean()
    {
        var raw = """
            { "orderId": "NO-1", "amount": "12.5", "qty": "3", "active": "true" }
            """;
        var coerced = AiDatabaseValueCoercer.Coerce(SimpleSchema, raw);
        Assert.Contains("\"amount\":12.5", coerced);
        Assert.Contains("\"qty\":3", coerced);
        Assert.Contains("\"active\":true", coerced);
    }

    [Fact]
    public void Coerce_ShouldThrowOnInvalidNumber()
    {
        var raw = """
            { "orderId": "NO-1", "amount": "abc" }
            """;
        Assert.Throws<BusinessException>(() => AiDatabaseValueCoercer.Coerce(SimpleSchema, raw));
    }

    [Fact]
    public void Coerce_ShouldThrowOnMissingRequired()
    {
        var raw = """
            { "amount": 1 }
            """;
        Assert.Throws<BusinessException>(() => AiDatabaseValueCoercer.Coerce(SimpleSchema, raw));
    }

    [Fact]
    public void Coerce_ShouldNormalizeIsoDate()
    {
        var raw = """
            { "orderId": "NO-1", "createdAt": "2026-04-18T10:00:00Z" }
            """;
        var coerced = AiDatabaseValueCoercer.Coerce(SimpleSchema, raw);
        Assert.Contains("\"createdAt\":\"2026-04-18T10:00:00.0000000Z\"", coerced);
    }

    [Fact]
    public void Coerce_OnArrayMismatch_ShouldThrow()
    {
        var raw = """
            { "orderId": "NO-1", "tags": "string-not-array" }
            """;
        Assert.Throws<BusinessException>(() => AiDatabaseValueCoercer.Coerce(SimpleSchema, raw));
    }
}
