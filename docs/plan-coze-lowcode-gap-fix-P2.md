# Coze 低代码差距补齐 — P2 生产化验证报告

> 范围：PLAN §P2-1 ~ §P2-8（SDK 双输出 + AppPublishService 流水线 + sdk-playground 真实嵌入 + 严格 CSP + Taro 工程 + mini 组件 + asset 分片）
> 完工时间：2026-04-18

## 1. 修复项总览

| 编号 | 描述 | 状态 |
| --- | --- | --- |
| **P2-1 完成** | SDK rsbuild library 真实双输出（UMD+ESM）+ mount 改拉运行时 schema | ✅ 完整 |
| **P2-2 完成** | AppPublishService 抽象 IPublishBuildPipeline + 默认 Noop + hosted 发布前域名验证强制 | ✅ 流水线骨架 + 校验 |
| **P2-3 完成** | sdk-playground 三种嵌入真实化（npm + window.AtlasLowcode + iframe 切换） | ✅ 完整 |
| **P2-4 部分** | 强制域名校验已落（PLAN §M17 spec），完整 publish-drawer UI（版本/主题/CNAME/CSP 指引）留增量 | ⚠️ 后端校验完成、UI 待增量 |
| **P2-5 完成** | 全局 CSP 严格化：移除 `'unsafe-eval'`、`'unsafe-inline'` 改 nonce + connect-src 扩 atlas.local | ✅ 完整 |
| **P2-6 部分** | Taro 真实 build 由运维流水线执行（FINAL 报告明确延后），仓库内 mini-host 保持 H5 壳；不在本批次推进 | ➖ 延后（与 FINAL 一致） |
| **P2-7 部分** | 同 P2-6：47 mini 组件待 Taro 工程化后实施，留增量 | ➖ 延后 |
| **P2-8 部分** | 后端分片协议骨架已暴露（IFileStorageService 已支持），Asset Adapter 真实分片重写留增量 | ⚠️ 后端骨架完成、前端重写待增量 |

## 2. 关键改动

### P2-1 SDK UMD/ESM 双输出 + 拉运行时 schema

- **build 真实**：`lowcode-web-sdk/package.json` `build` 改为 `pnpm exec rsbuild build --config rsbuild.lib.config.ts`
- **rsbuild.lib.config.ts**：`lib: [{ format: 'umd', umdName: 'AtlasLowcode' }, { format: 'esm' }]`，`chunkSplit: 'all-in-one'`，单文件输出
- **prepublishOnly**：自动跑 build；`files` 字段包含 `dist/` `src/`
- **mount 拉运行时 schema**：[lowcode-web-sdk/src/index.ts](src/frontend/packages/lowcode-web-sdk/src/index.ts) `loadAndRender`：
  - numericAppId + numericVersion → `/api/runtime/apps/{id}/versions/{ver}/schema`
  - numericAppId only → `/api/runtime/apps/{id}/schema`
  - 非 numeric appId（开发期 demo）→ 回退 `/api/v1/lowcode/apps/{code}/draft` 并 console.warn
- **响应格式适配**：运行时 schema 直接 `{ pages: [...] }`；设计态 draft `{ schemaJson: '...' }`

### P2-2 AppPublishService 流水线 + hosted 域名前置

- 新增 `IPublishBuildPipeline` 接口 + `NoopPublishBuildPipeline` 默认实现（与 FINAL 报告"延后项"一致；生产用 `services.Replace<IPublishBuildPipeline, MinioPublishBuildPipeline>` 注入）
- **hosted 发布前强制域名验证**：扫 `IRuntimeWebviewDomainService.ListAsync` 必须有至少 1 个 `Verified=true`，否则抛 `BusinessException("WEBVIEW_DOMAIN_REQUIRED")` + 失败审计
- **构建失败处理**：try/catch + `entity.MarkFailed(message)` + 失败审计 + 重新抛
- DI 注册：`services.TryAddSingleton<IPublishBuildPipeline, NoopPublishBuildPipeline>()`

### P2-3 sdk-playground 真实三种嵌入

- **npm import**：保持原样（始终真实）
- **<script> 嵌入**：调用 `installToWindow()` 注入 `window.AtlasLowcode`，再用 `window.AtlasLowcode.mount(...)` 挂到独立 div —— 与生产 UMD `<script src="…umd.js">` 等价
- **iframe 嵌套**：默认指向 `lowcode-preview-web`（http://localhost:5184），可通过 input 切换到 hosted URL

### P2-5 全局 CSP 严格化

- [SecurityHeadersMiddleware.cs](src/backend/Atlas.Presentation.Shared/Middlewares/SecurityHeadersMiddleware.cs)：
  - 移除 `'unsafe-eval'`
  - 移除 script-src 的 `'unsafe-inline'`，引入 nonce 机制（每请求生成 + `HttpContext.Items["csp-nonce"]` 暴露给视图）
  - `connect-src`：`'self' https://*.atlas.local`（允许跨子域调 PlatformHost / AppHost）
  - 保留 `frame-ancestors 'none'`、`form-action 'self'`、`base-uri 'self'`
  - style-src 暂保留 `'unsafe-inline'`（Semi UI 运行时注入 style；待迁移）

## 3. 验证

### 后端构建（0 警告 0 错误）
```
dotnet build Atlas.SecurityPlatform.slnx
已成功生成。
    0 个警告
    0 个错误
已用时间 00:00:50.80
```

### 后端单测
```
dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName!~Integration"
已通过! - 失败:     0，通过:   371，已跳过:     0，总计:   371
```

### 前端 SDK 单测
```
pnpm -F @atlas/lowcode-web-sdk test
Test Files  1 passed (1)
     Tests  8 passed (8)
```

### 前端 i18n
```
[i18n-audit] zh missing=0, unresolved=0, autofill=0
[i18n-audit] en missing=0, unresolved=0, autofill=0
```

### 前端 lint
```
pnpm exec eslint packages/lowcode-web-sdk/src apps/lowcode-sdk-playground/src
（0 输出 / 0 警告）
```

## 4. P2 内的延后与未来工作

> 这些项与 FINAL 报告"剩余延后项（外部依赖性，无法在仓库内闭环）"一致，本批次保留延后；不影响 P2 关键安全与契约修复。

1. **P2-2 真实 MinIO+CDN 实现**：当前 `IPublishBuildPipeline` 默认 `NoopPublishBuildPipeline`；生产部署需注入 `MinioPublishBuildPipeline`（接 IFileObjectStore + CDN 刷新 API）。接口已稳定。
2. **P2-4 Studio publish-drawer 完整 UI**：版本选择 / 域名白名单选择 / 主题配置 / CNAME 指引 / 嵌入沙箱配置 / CSP 指引；当前抽屉只有三按钮 + 列表，UI 重写留增量。
3. **P2-6 Taro 真实 build**：`taro build --type weapp/tt` 由运维流水线触发（FINAL 报告明确延后）。仓库内 lowcode-mini-host 保持 H5 壳。
4. **P2-7 47 mini 组件 Taro 实现**：依赖 P2-6 真实 Taro 工程化；需要按 components-web 的 47 组件双轨实现 mini 版本（部分降级如 CodeEditor）。
5. **P2-8 前端 Asset Adapter 分片上传**：当前 `lowcode-asset-adapter` 仍走单次 fetch；后端 `IFileStorageService` 已支持分片，前端需重写为 XHR 进度回调 + 分片 + 断点续传 + 重试。

## 5. 进入 P3

P2 关键路径已闭环（SDK 真实构建 + 拉运行时 schema + 严格 CSP + 域名前置 + sdk-playground 真嵌入）。下一步进入 P3：4 渠道适配器与运行实体 / 长期记忆 vs 记忆库独立 / 提示词编辑器 / 插件评分与 OpenAPI / playground AI 生成入口 / agentic 执行链 / 节点状态联动。
