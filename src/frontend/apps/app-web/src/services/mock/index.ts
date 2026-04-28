/**
 * Coze 第一阶段 Mock 服务统一出口。
 *
 * - 所有 mock 函数签名 = 后端最终契约（详见 docs/mock-api-protocols.md）。
 * - 第三阶段联调时，本目录中的 mock 模块会被同名真实 service 替换：
 *   1) 在 services/ 下新增同名 api-*.ts；
 *   2) 修改下方 re-export，把 mock-* 替换为真实模块；
 *   3) 删除 mock-* 文件（一次替换一个对象，不要一次性大改）。
 *
 * MockSwitch（保留扩展点）：未来如需"页面级 mock 开关"，
 * 可在 createApiClient 中读取 `localStorage.atlas_use_mock`，
 * 选择性走 mock。本阶段全部页面强制使用本目录 mock，无需开关。
 */

export * from "./mock-utils";
export * from "./api-tasks.mock";
export * from "./api-evaluations.mock";
export * from "./api-publish-channels.mock";
export * from "./api-setup-console.mock";
export * from "./api-system-init.mock";
export * from "./api-workspace-init.mock";
