using SqlSugar;

namespace Atlas.Infrastructure.Setup;

/// <summary>
/// Setup 专用数据库客户端工厂。
/// 在 setup 未完成（IsReady = false）时提供 ISqlSugarClient 实例，
/// 绕开正常业务态 ISqlSugarClient 的 IsReady 门禁。
/// </summary>
public interface ISetupDbClientFactory
{
    ISqlSugarClient Create(string connectionString, string dbType);
}
