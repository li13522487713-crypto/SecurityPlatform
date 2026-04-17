import type { ActionSchema } from './action';

/**
 * LifecycleSchema —— 页面生命周期（docx §10.2.2）。
 *
 * 三阶段：beforePageLoad / afterPageLoad / beforePageUnload。
 */
export interface LifecycleSchema {
  beforePageLoad?: ActionSchema[];
  afterPageLoad?: ActionSchema[];
  beforePageUnload?: ActionSchema[];
}
