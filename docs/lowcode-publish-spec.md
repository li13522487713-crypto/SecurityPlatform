# 低代码发布规格（lowcode-publish-spec）

> 状态：M17 落地。
> 范围：三类产物 + Web SDK API 契约 + 安全配置 + 端点双套（设计态 v1 + 运行时只读）。

## 1. 三类产物总览

| Kind | URL 模板 | 用途 |
| --- | --- | --- |
| `hosted` | `https://apps.atlas.local/{code}` | 独立托管应用；CNAME 配置由域名管理面板（M12 webview-policy）联动；发布后自动 `MarkPublished` 应用。 |
| `embedded-sdk` | `https://cdn.atlas.local/sdk/{fingerprint}/atlas-lowcode.umd.js` | 通过 `<script>` 嵌入第三方页面；产物指纹与版本绑定。 |
| `preview` | `https://preview.atlas.local/{code}?v={fingerprint}` | 仅内部调试可见；扫码移动端预览（M08 联动）。 |

## 2. Web SDK API（@atlas/lowcode-web-sdk）

```ts
import { mount, type MountInstance } from '@atlas/lowcode-web-sdk';

const inst: MountInstance = mount({
  container: '#root' | HTMLElement,
  appId: 'demo',
  version: 'v1.0.0',
  initialState: { page: { count: 0 }, app: { user: {...} } },
  theme: { primaryColor: '#1677ff', borderRadius: 4, darkMode: 'auto' },
  onEvent: (e) => console.log(e),
  baseUrl: '/',
  tenantId: '...',
  token: '...'
});

inst.update([{ scope: 'page', path: 'page.count', op: 'set', value: 5 }]);
inst.getState();   // { page, app, component }
inst.unmount();
```

`installToWindow()`：将 `mount` 注入到 `window.AtlasLowcode`，兼容 `<script>` 嵌入。

## 3. 端点双套

### 设计态（PlatformHost / `/api/v1/lowcode/apps/{id}`）
- `POST /publish/{kind}`            发布（hosted / embedded-sdk / preview）
- `GET  /artifacts`                 列出产物
- `POST /publish/rollback`          按 artifactId 撤回

### 运行时只读（AppHost / `/api/runtime/publish/{appId}`）
- `GET  /artifacts`                 列出产物（只读，不含密钥字段）

## 4. 产物指纹

`SHA256(kind | versionId | schemaJson)`，与版本绑定；前端 SDK 加载时按指纹做版本一致性校验。

## 5. 外链白名单（M12 + M17 联动）

- 发布 hosted 类型必须先在 webview-domains 注册并验证（dns_txt / http_file）。
- runtime 端点 `open_external_link` 由 lowcode-webview-policy-adapter `isAllowed` 校验；未通过的域名直接拒绝跳转。

## 6. 三种嵌入示例

参见 `apps/lowcode-sdk-playground/src/main.tsx`：

1. **npm import**：直接 `mount(...)` 到 React ref；
2. **`<script>` 嵌入**：`installToWindow()` + `<script src="...atlas-lowcode.umd.js"></script>` + `AtlasLowcode.mount(...)`；
3. **iframe 嵌套**：在父页面 `<iframe src="https://apps.atlas.local/{code}">`。

## 7. 等保 2.0

- 发布全链路审计（`lowcode.app.publish` / `lowcode.app.publish.revoke`）。
- 产物指纹与版本绑定，撤回保留状态变更链路。
- SDK 加载来源校验：建议在父页面 CSP 加 `script-src 'self' https://cdn.atlas.local`。
- CSP 严格策略：`default-src 'self'; connect-src 'self' https://*.atlas.local`。

## 8. 反例

- 直接修改 `AppPublishArtifact.PublicUrl` 跳过 publish 流程 —— 拒绝；必须经 `IAppPublishService.PublishAsync` 重新计算指纹。
- `<script>` 引入未在白名单 CDN 的 SDK 文件 —— CSP 阻断。

## 9. P2 对齐说明（2026-04 P5-1 修正）

| spec 条款 | 代码实现位置 | P2 修复点 |
|---|---|---|
| 发布 hosted 必须先验证域名 | `AppPublishService.PublishAsync` | P2-2：扫 `IRuntimeWebviewDomainService.ListAsync` 必须有 ≥1 个 `Verified=true`，否则抛 `WEBVIEW_DOMAIN_REQUIRED` |
| 严格 CSP（去 unsafe-eval） | `SecurityHeadersMiddleware` | P2-5：移除 `'unsafe-eval'`，script-src 引入 nonce 机制；`connect-src 'self' https://*.atlas.local` |
| SDK UMD/ESM 双输出 | `lowcode-web-sdk/rsbuild.lib.config.ts` | P2-1：rsbuild library 模式真实双输出；prepublishOnly 自动 build |
| SDK 拉运行时 schema | `lowcode-web-sdk/src/index.ts` | P2-1：numeric appId → /api/runtime/apps/{id}/schema；非 numeric → 回退 design draft + console.warn |
| sdk-playground 真实三种嵌入 | `apps/lowcode-sdk-playground/src/main.tsx` | P2-3：npm + window.AtlasLowcode + iframe 切换 |
| 构建流水线抽象 | `IPublishBuildPipeline` 接口 | P2-2：默认 NoopPublishBuildPipeline；生产用 `services.Replace<IPublishBuildPipeline, MinioPublishBuildPipeline>` |
