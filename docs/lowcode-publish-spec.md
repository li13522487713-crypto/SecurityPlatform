# 低代码发布规格（lowcode-publish-spec）

> 状态：M00 预创建 stub。
> 范围：M17 三类发布产物（Hosted App / Embedded SDK / Preview Artifact）完整发布流程 + Web SDK API 契约 + 安全配置。

## 章节占位

- §1 三类产物总览
  - Hosted App：`https://apps.atlas.local/{appId}` + CNAME 配置指引
  - Embedded SDK：`<script>` 嵌入 + 沙箱 + CSP 指引
  - Preview Artifact：内部调试 + 二维码 + 移动端预览
- §2 `AtlasLowcode.mount({ container, appId, version, initialState, theme, onEvent })` 完整 API（含 `unmount` / `update` / `getState`）
- §3 产物指纹（SHA256） + 版本绑定 + 渲染器矩阵绑定
- §4 设计态 `AppPublishController` 端点全集（v1）
- §5 运行时只读 `GET /api/runtime/publish/{appId}/artifacts`
- §6 外链白名单 `RuntimeWebviewDomainsController` 验证流程（DNS TXT / HTTP 文件）
- §7 等保 2.0：发布全链路审计 / SDK 加载来源校验 / CSP 严格策略

> 完整内容由 M17 落地。
