using Atlas.Application.Abstractions;
using Atlas.Application.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 图形验证码服务（等保2.0：防暴力破解）
/// 使用内存缓存存储答案，返回 SVG 格式图片以避免引入额外依赖。
/// </summary>
public sealed class CaptchaService : ICaptchaService
{
    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 4;

    private readonly IMemoryCache _cache;
    private readonly SecurityOptions _securityOptions;

    public CaptchaService(IMemoryCache cache, IOptions<SecurityOptions> securityOptions)
    {
        _cache = cache;
        _securityOptions = securityOptions.Value;
    }

    public (string CaptchaKey, string Base64Image) Generate()
    {
        var code = GenerateCode();
        var key = $"captcha:{Guid.NewGuid():N}";
        var expiry = TimeSpan.FromSeconds(_securityOptions.CaptchaExpirySeconds > 0
            ? _securityOptions.CaptchaExpirySeconds
            : 300);

        _cache.Set(key, code.ToUpperInvariant(), expiry);

        var svg = GenerateSvg(code);
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        return (key, $"data:image/svg+xml;base64,{base64}");
    }

    public bool Validate(string captchaKey, string captchaCode)
    {
        if (string.IsNullOrWhiteSpace(captchaKey) || string.IsNullOrWhiteSpace(captchaCode))
            return false;

        if (!_cache.TryGetValue<string>(captchaKey, out var stored) || stored is null)
            return false;

        _cache.Remove(captchaKey);
        return string.Equals(stored, captchaCode.Trim().ToUpperInvariant(), StringComparison.Ordinal);
    }

    private static string GenerateCode()
    {
        var chars = new char[CodeLength];
        var bytes = RandomNumberGenerator.GetBytes(CodeLength);
        for (var i = 0; i < CodeLength; i++)
        {
            chars[i] = Chars[bytes[i] % Chars.Length];
        }
        return new string(chars);
    }

    private static string GenerateSvg(string code)
    {
        var rng = new Random();
        var sb = new StringBuilder();
        sb.Append("""<svg xmlns="http://www.w3.org/2000/svg" width="120" height="40">""");
        sb.Append("""<rect width="120" height="40" fill="#f0f2f5"/>""");

        // 干扰线
        for (var i = 0; i < 4; i++)
        {
            var x1 = rng.Next(0, 120);
            var y1 = rng.Next(0, 40);
            var x2 = rng.Next(0, 120);
            var y2 = rng.Next(0, 40);
            var color = $"rgb({rng.Next(150, 200)},{rng.Next(150, 200)},{rng.Next(150, 200)})";
            sb.Append($"""<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{color}" stroke-width="1"/>""");
        }

        // 字符
        var colors = new[] { "#1677ff", "#52c41a", "#fa8c16", "#f5222d", "#722ed1" };
        for (var i = 0; i < code.Length; i++)
        {
            var x = 10 + i * 27 + rng.Next(-3, 3);
            var y = 26 + rng.Next(-4, 4);
            var rotate = rng.Next(-15, 15);
            var color = colors[rng.Next(colors.Length)];
            var fontSize = 18 + rng.Next(-2, 2);
            sb.Append($"""<text x="{x}" y="{y}" font-family="Arial" font-size="{fontSize}" fill="{color}" transform="rotate({rotate},{x},{y})" font-weight="bold">{code[i]}</text>""");
        }

        // 干扰点
        for (var i = 0; i < 30; i++)
        {
            var cx = rng.Next(0, 120);
            var cy = rng.Next(0, 40);
            var color = $"rgb({rng.Next(100, 200)},{rng.Next(100, 200)},{rng.Next(100, 200)})";
            sb.Append($"""<circle cx="{cx}" cy="{cy}" r="1.5" fill="{color}"/>""");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }
}
