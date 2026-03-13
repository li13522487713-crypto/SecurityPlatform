// API 入口：re-export 各领域子模块，保持向后兼容的导入路径
// 各领域子模块：
//   api-auth.ts        认证、令牌、用户资料、文件上传
//   api-users.ts       用户/部门/角色/权限/菜单/职位/告警
//   api-approval.ts    审批流/实例/任务/代理人/部门负责人
//   api-workflow.ts    WorkflowCore 引擎
//   api-visualization.ts  可视化流程监控
//   api-system.ts      应用配置/项目/数据源/表格视图

export type { RequestOptions } from "@/services/api-core";
export { requestApi } from "@/services/api-core";

export * from "@/services/api-auth";
export * from "@/services/api-users";
export * from "@/services/api-approval";
export * from "@/services/api-workflow";
export * from "@/services/api-visualization";
export * from "@/services/api-system";
export * from "@/services/api-plugin";
export * from "@/services/api-webhook";
export * from "@/services/api-license";
export * from "@/services/api-productization";
export * from "@/services/api-model-config";
export * from "@/services/api-agent";
export * from "@/services/api-ai-database";
export * from "@/services/api-ai-variable";
export * from "@/services/api-ai-plugin";
export * from "@/services/api-ai-app";
export * from "@/services/api-ai-prompt";
export * from "@/services/api-ai-marketplace";
export * from "@/services/api-ai-search";
export * from "@/services/api-admin-ai-config";
export * from "@/services/api-pat";
