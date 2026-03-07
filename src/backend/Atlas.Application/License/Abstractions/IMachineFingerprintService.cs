namespace Atlas.Application.License.Abstractions;

/// <summary>
/// 机器指纹服务：采集并提供当前设备的唯一标识。
/// </summary>
public interface IMachineFingerprintService
{
    /// <summary>获取主指纹（主板序列号 + CPU ID 等组合哈希）</summary>
    string GetCurrentFingerprint();

    /// <summary>判断给定指纹是否与当前机器匹配（含容错逻辑）</summary>
    bool Matches(string? storedFingerprint);
}
