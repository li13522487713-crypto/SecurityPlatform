# 低代码插件全域规格（lowcode-plugin-spec）

> 状态：M00 预创建 stub。
> 范围：M18 智能体 + 工作流 N10 节点共享的插件市场 / 创建 / 调用 / 授权 / 计量 / 发布完整域。

## 章节占位

- §1 插件总览：与现有工作流 N10 插件节点共享 `PluginRegistry`
- §2 插件市场（浏览 / 检索 / 评分 / 安装）
- §3 插件创建（OpenAPI 导入 / 手动定义工具 / 测试调用）
- §4 插件调用（智能体内调用 + 工作流 N10 节点调用）
- §5 插件授权（OAuth / API Key / 租户级权限）
- §6 插件计量（调用次数 / 配额 / 计费）
- §7 插件发布（私有 / 公开 / 团队）
- §8 后端域：`Atlas.Domain.LowCodePlugin`（`PluginDefinition` / `PluginVersion` / `PluginAuthorization` / `PluginUsage` 4 个 TenantEntity）
- §9 设计态端点：`PluginsController`（`/api/v1/lowcode/plugins`）
- §10 运行时端点：`POST /api/runtime/plugins/{id}:invoke`
- §11 前端 adapter：`@atlas/lowcode-plugin-adapter`

> 完整内容由 M18 落地。
