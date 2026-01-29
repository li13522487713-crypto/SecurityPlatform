using Atlas.Core.Tenancy;
using Atlas.WorkflowCore;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Abstractions.Persistence;
using Atlas.WorkflowDemo.Infrastructure;
using Atlas.WorkflowDemo.Models;
using Atlas.WorkflowDemo.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Atlas.WorkflowDemo;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=========================================");
        Console.WriteLine("  Atlas WorkflowCore - 审批流Demo示例  ");
        Console.WriteLine("=========================================");
        Console.WriteLine();

        // 配置依赖注入
        var services = new ServiceCollection();

        // 添加日志（控制台输出）
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning); // 减少日志输出，只显示警告及以上级别
        });

        // 添加WorkflowCore核心服务
        services.AddWorkflowCore();

        // 注册WorkflowOptions配置
        services.AddSingleton(new Atlas.WorkflowCore.Models.WorkflowOptions
        {
            PollInterval = TimeSpan.FromSeconds(1), // 缩短轮询间隔以便快速执行
            IdleTime = TimeSpan.FromMilliseconds(100),
            EnablePolling = true,
            EnableWorkflows = true,
            EnableEvents = true
        });

        // 注册内存持久化提供者（单例）
        services.AddSingleton<IPersistenceProvider, InMemoryPersistenceProvider>();

        // 注册对象池策略（用于后台任务）
        services.AddSingleton<IPooledObjectPolicy<IPersistenceProvider>>(sp => 
            new DemoPersistenceProviderPoolPolicy(sp));

        // 注册租户提供者（Demo用固定租户）
        services.AddSingleton<ITenantProvider, DemoTenantProvider>();

        // 构建服务提供者
        var serviceProvider = services.BuildServiceProvider();

        // 获取工作流宿主
        var workflowHost = serviceProvider.GetRequiredService<IWorkflowHost>();

        try
        {
            // 启动工作流宿主
            Console.WriteLine("[系统] 正在启动工作流引擎...");
            await workflowHost.StartAsync();
            Console.WriteLine("[系统] 工作流引擎启动成功！");
            Console.WriteLine();

            // 注册工作流定义
            workflowHost.RegisterWorkflow<SimpleApprovalWorkflow, ApprovalWorkflowData>();
            Console.WriteLine("[系统] 工作流定义已注册: simple-approval (v1)");
            Console.WriteLine();

            // 创建工作流数据
            var workflowData = new ApprovalWorkflowData
            {
                ApplicationTitle = "采购申请-办公设备",
                Applicant = "张三"
            };

            // 启动工作流实例
            Console.WriteLine("[系统] 正在启动工作流实例...");
            var instanceId = await workflowHost.StartWorkflowAsync(
                workflowId: "simple-approval",
                version: null, // 使用最新版本
                data: workflowData,
                reference: "REF-2026-001"
            );

            Console.WriteLine($"[系统] 工作流实例已启动");
            Console.WriteLine($"  实例ID: {instanceId}");
            Console.WriteLine($"  引用编号: REF-2026-001");
            Console.WriteLine();

            // 等待工作流执行完成
            Console.WriteLine("[系统] 等待工作流执行...");
            await Task.Delay(5000); // 等待5秒让工作流执行完成

            // 查询工作流实例状态
            var persistenceProvider = serviceProvider.GetRequiredService<IPersistenceProvider>();
            var instance = await persistenceProvider.GetWorkflowAsync(instanceId);
            if (instance != null)
            {
                Console.WriteLine();
                Console.WriteLine("[系统] 工作流执行完成！");
                Console.WriteLine($"  最终状态: {instance.Status}");
                Console.WriteLine($"  创建时间: {instance.CreateTime:yyyy-MM-dd HH:mm:ss}");
                if (instance.CompleteTime.HasValue)
                {
                    Console.WriteLine($"  完成时间: {instance.CompleteTime.Value:yyyy-MM-dd HH:mm:ss}");
                    var duration = instance.CompleteTime.Value - instance.CreateTime;
                    Console.WriteLine($"  执行耗时: {duration.TotalMilliseconds:F0}ms");
                }
                Console.WriteLine();
            }

            // 停止工作流宿主
            Console.WriteLine("[系统] 正在停止工作流引擎...");
            await workflowHost.StopAsync();
            Console.WriteLine("[系统] 工作流引擎已停止。");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"[错误] 发生异常: {ex.Message}");
            Console.WriteLine($"[错误] 堆栈跟踪: {ex.StackTrace}");
        }

        Console.WriteLine();
        Console.WriteLine("=========================================");
        Console.WriteLine("  按任意键退出...  ");
        Console.WriteLine("=========================================");
        Console.ReadKey();
    }
}
