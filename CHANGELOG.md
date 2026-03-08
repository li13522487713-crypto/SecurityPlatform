# Changelog

## platform-v1.0-baseline

- 平台标准收口：新增 `docs/platform-unified-schema-and-expression.md`，并在 `docs/contracts.md` 固化 Schema/CEL/上下文与发布态规则。
- 运行态闭环：页面发布/下线自动联动 `RuntimeRoute`，新增运行态菜单 API，运行态任务查询与动作执行改为真实服务调用。
- 工作台闭环：`AppManifest` 的 `workspace/pages|forms|flows|data|permissions` 从占位返回改为真实数据聚合。
- 治理闭环：`PackageService` 支持 ZIP 导出/导入/冲突分析，`LicenseGrantService` 支持授权文件解析、签名校验、机器指纹匹配。
- 交付工程化：前端新增 `npm run generate-api`（复用 NSwag），完善运行态与治理基线 `.http` 验证脚本。

