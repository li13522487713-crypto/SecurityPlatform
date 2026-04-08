namespace Atlas.Application.Abstractions;

public interface ICaptchaService
{
    /// <summary>生成验证码，返回 (captchaKey, base64Image)</summary>
    Task<(string CaptchaKey, string Base64Image)> GenerateAsync();

    /// <summary>校验验证码（不区分大小写），校验成功后自动使 key 失效</summary>
    Task<bool> ValidateAsync(string captchaKey, string captchaCode);
}
