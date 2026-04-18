import type { ScopeRoot, ValueType } from '../shared/enums';
import type { JsonValue } from '../shared/json';

/**
 * VariableSchema —— 变量定义（docx §10.2.3）。
 *
 * page 级变量内联在 PageSchema.variables，
 * app / system 级变量在 AppSchema.variables。
 */
export interface VariableSchema {
  code: string;
  displayName: string;
  scope: Extract<ScopeRoot, 'page' | 'app' | 'system'>;
  valueType: ValueType;
  /** 是否只读（system 强制 true）。*/
  readonly?: boolean;
  /** 是否持久化（跨会话保留，true 时落 KV 存储）。*/
  persist?: boolean;
  /** 默认值（任意 JSON）。*/
  defaultValue?: JsonValue;
  /** 校验规则（zod 或自定义 JSON schema 序列化）。*/
  validation?: JsonValue;
  description?: string;
}
