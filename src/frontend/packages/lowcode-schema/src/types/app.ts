import type { PageSchema } from './page';
import type { VariableSchema } from './variable';
import type { ContentParamSchema } from './content-param';
import type { JsonObject } from '../shared/json';
import type { AppStatus, SchemaVersion, TargetType } from '../shared/enums';

/**
 * AppSchema —— 应用 schema 顶层（docx §10.2.1）。
 */
export interface AppSchema {
  schemaVersion: SchemaVersion;
  appId: string;
  code: string;
  displayName: string;
  description?: string;
  /** 多端类型（逗号分隔的字符串数组在前端做枚举值校验）。*/
  targetTypes: TargetType[];
  defaultLocale: string;
  status?: AppStatus;
  /** 应用级变量（app + system 作用域）。*/
  variables?: VariableSchema[];
  /** 应用级内容参数（跨页面共享，与 page 级 contentParams 区分）。*/
  contentParams?: ContentParamSchema[];
  /** 页面集合。*/
  pages: PageSchema[];
  theme?: AppTheme;
  metadata?: JsonObject;
}

export interface AppTheme {
  primaryColor?: string;
  borderRadius?: number;
  darkMode?: 'never' | 'always' | 'auto';
  cssVariables?: Record<string, string>;
}
