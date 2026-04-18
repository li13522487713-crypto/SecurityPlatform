using System.Text.Json;
using Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

namespace Atlas.SecurityPlatform.Tests.Services.AiPlatform;

public sealed class AiNodeObservabilityTests
{
    [Fact]
    public void MaskString_ShouldKeepEdgesAndStarMiddle()
    {
        Assert.Equal("**", AiNodeObservability.MaskString("a"));
        Assert.Equal("**", AiNodeObservability.MaskString("ab"));
        // 长度 ≤ 6：保留首末各 1 字符。
        Assert.Equal("a*c", AiNodeObservability.MaskString("abc"));
        Assert.Equal("a****f", AiNodeObservability.MaskString("abcdef"));
        // 长度 > 6：保留首末各 2 字符。
        Assert.Equal("ab***fg", AiNodeObservability.MaskString("abcdefg"));
    }

    [Fact]
    public void MaskEmail_ShouldOnlyMaskLocalPart()
    {
        // alice -> 长度 5（≤6）→ 首末 1 字符：a***e；domain 保留。
        var masked = AiNodeObservability.MaskEmail("alice@example.com");
        Assert.Equal("a***e@example.com", masked);
    }

    [Fact]
    public void MaskPhone_ShouldKeepFirst3AndLast2()
    {
        Assert.Equal("138******21", AiNodeObservability.MaskPhone("13800000021"));
        Assert.Equal("****", AiNodeObservability.MaskPhone("1234"));
    }

    [Fact]
    public void Mask_OnObject_ShouldHideSensitiveFields()
    {
        var input = JsonSerializer.SerializeToElement(new
        {
            id = 1,
            password = "verysecret",
            apiKey = "sk-12345",
            note = "alice@example.com",
            mobile = "13800000021",
            payload = new { token = "tk-xx", remark = "ok" }
        });
        var masked = AiNodeObservability.Mask(input);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(masked.GetRawText())!;
        Assert.Equal(1, dict["id"].GetInt32());
        Assert.NotEqual("verysecret", dict["password"].GetString());
        Assert.NotEqual("sk-12345", dict["apiKey"].GetString());
        // note 字段名非敏感，但值匹配 email pattern → 局部脱敏。
        Assert.Equal("a***e@example.com", dict["note"].GetString());
        // mobile 字段名 + 值都触发，按字段名优先（MaskString），仍是 mask 后值。
        Assert.NotEqual("13800000021", dict["mobile"].GetString());

        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dict["payload"].GetRawText())!;
        Assert.NotEqual("tk-xx", payload["token"].GetString());
        Assert.Equal("ok", payload["remark"].GetString());
    }

    [Fact]
    public void Mask_OnObject_ShouldZeroSensitiveNumericFields()
    {
        var input = JsonSerializer.SerializeToElement(new { ssn = 12345, ok = 42 });
        var masked = AiNodeObservability.Mask(input);
        Assert.Equal(0, masked.GetProperty("ssn").GetInt32());
        Assert.Equal(42, masked.GetProperty("ok").GetInt32());
    }

    [Fact]
    public void Mask_OnArray_ShouldRecurseEachItem()
    {
        var input = JsonSerializer.SerializeToElement(new[]
        {
            new { name = "alice", token = "tk-1" },
            new { name = "bob", token = "tk-2" }
        });
        var masked = AiNodeObservability.Mask(input);
        var arr = masked.EnumerateArray().ToList();
        Assert.Equal(2, arr.Count);
        Assert.Equal("alice", arr[0].GetProperty("name").GetString());
        Assert.NotEqual("tk-1", arr[0].GetProperty("token").GetString());
        Assert.NotEqual("tk-2", arr[1].GetProperty("token").GetString());
    }
}
