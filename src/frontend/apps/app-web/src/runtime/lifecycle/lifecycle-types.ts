/**
 * 页面生命周期钩子类型定义。
 *
 * Phase 1 定义接口，Phase 3 实现完整的 onPageInit / beforeSubmit / afterSubmit。
 */

import type { RuntimeAction } from "../actions/action-types";

export interface PageLifecycleHooks {
  onPageInit?: RuntimeAction[];
  beforeSubmit?: RuntimeAction[];
  afterSubmit?: RuntimeAction[];
  onPageLeave?: RuntimeAction[];
  onError?: RuntimeAction[];
}
