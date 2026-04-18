import type { ComponentSchema } from './component';
import type { VariableSchema } from './variable';
import type { LifecycleSchema } from './lifecycle';
import type { ContentParamSchema } from './content-param';
import type { JsonObject } from '../shared/json';
import type { PageLayout, TargetType } from '../shared/enums';

/**
 * PageSchema —— 单页面 schema（docx §10.2.2）。
 */
export interface PageSchema {
  id: string;
  code: string;
  displayName: string;
  path: string;
  targetType: TargetType;
  layout: PageLayout;
  /** 排序（应用内）。*/
  orderNo?: number;
  visible?: boolean;
  locked?: boolean;
  /** 根组件（必填）。*/
  root: ComponentSchema;
  /** page 级变量（与 app / system 级变量隔离）。*/
  variables?: VariableSchema[];
  /** page 级内容参数实例（与组件级 contentParams 区分）。*/
  contentParams?: ContentParamSchema[];
  lifecycle?: LifecycleSchema;
  metadata?: JsonObject;
}
