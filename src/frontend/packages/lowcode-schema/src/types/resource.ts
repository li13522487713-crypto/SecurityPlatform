/**
 * ResourceRef —— 资源引用统一抽象（docx §10.8 资源引用治理 + M14 反查）。
 *
 * 用于在 schema 中引用工作流 / 对话流 / 变量 / 触发器 / 数据源 / 插件 / 提示词模板等。
 */
export interface ResourceRef {
  resourceType:
    | 'workflow'
    | 'chatflow'
    | 'variable'
    | 'trigger'
    | 'datasource'
    | 'plugin'
    | 'prompt-template'
    | 'knowledge'
    | 'database'
    | 'file';
  resourceId: string;
  /** 被引用资源的版本号（可选，绑定到具体版本以支持回滚）。*/
  version?: string;
}
