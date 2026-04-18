# 低代码插件全域规格（lowcode-plugin-spec）

> 状态：M18 落地。
> 范围：M18 智能体 + 工作流 N10 节点共享的插件市场 / 创建 / 调用 / 授权 / 计量 / 发布完整域。

## 1. 总览

- 与现有工作流 N10（`Plugin`）插件节点共享 PluginRegistry。
- 设计态：`/api/v1/lowcode/plugins`（PlatformHost）；运行时：`/api/runtime/plugins/{id}:invoke`（AppHost）。
- 插件域 4 张表：
  - `LowCodePluginDefinition`（市场可见性 / latestVersion / toolsJson）
  - `LowCodePluginVersion`（版本归档）
  - `LowCodePluginAuthorization`（api_key / oauth / basic / none，credentialEncrypted 经 LowCodeCredentialProtector AES-CBC + 'lcp:' 前缀加密）
  - `LowCodePluginUsage`（按日聚合 invocationCount / errorCount）

## 2. 设计态端点

| 方法 | 路径 | 说明 |
| --- | --- | --- |
| GET | `/api/v1/lowcode/plugins` | 搜索（keyword + shareScope）|
| POST | `/api/v1/lowcode/plugins` | upsert（toolsJson 服务端 JSON 校验）|
| DELETE | `/api/v1/lowcode/plugins/{id}` | 删除 |
| POST | `/api/v1/lowcode/plugins/{id}/publish` | 发布版本（bumpVersion + insertVersion 归档）|
| POST | `/api/v1/lowcode/plugins/{pluginId}/authorize` | 配置授权 |
| GET | `/api/v1/lowcode/plugins/{pluginId}/usage?day=YYYY-MM-DD` | 查询某日使用量 |

## 3. 运行时端点

| 方法 | 路径 | 说明 |
| --- | --- | --- |
| POST | `/api/runtime/plugins/{pluginId}:invoke` | 调用插件工具；自动计量 + 审计 |

## 4. 提示词模板（M18 跨域资源）

`/api/v1/lowcode/prompt-templates`：

| 方法 | 路径 | 说明 |
| --- | --- | --- |
| GET | / | 搜索（keyword）|
| POST | / | upsert（jinja / markdown / plain 三种 mode；shareScope 三档）|
| DELETE | /{id} | 删除 |

## 5. 等保 2.0

- `credentialEncrypted` 字段已经 **`LowCodeCredentialProtector`（AES-CBC + 'lcp:' 前缀幂等）** 加密（M18 收尾，2026-04）：
  - 主密钥优先级：`Security:LowCode:CredentialProtectorKey` → `Security:SetupConsole:MigrationProtectorKey` → `Security:BootstrapAdmin:Password` → DefaultDevKey（仅开发环境）。
  - 重复加密幂等；旧 base64 无前缀值在解密时原样返回（向后兼容），下次写入自动升级为带前缀密文。
  - 静态 `Mask(value)` 方法用于审计/日志（前 4 + 后 2，中间 ****）。
- 写接口全部经 IAuditWriter（lowcode.plugin.create/update/delete/publish/authorize/invoke）。
  - 审计记录的 `target` 字段使用 Mask 摘要，不写明文 / 密文。
- 计量记录用于配额治理（M19）。

## 6. 反例

- 直接修改 `LowCodePluginVersion.ToolsJson` 跳过 publish 流程 —— 拒绝；必须经 `IAppPublishService` / `LowCodePluginService.PublishVersionAsync` 重新归档。
- 在工作流节点内直 fetch 插件 URL —— 必须经 `RuntimePluginsController.Invoke`，由插件域统一计量与审计。
