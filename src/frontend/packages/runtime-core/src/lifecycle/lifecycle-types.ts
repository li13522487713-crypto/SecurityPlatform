import type { RuntimeAction } from "../actions/action-types";

export interface PageLifecycleHooks {
  onPageInit?: RuntimeAction[];
  beforeSubmit?: RuntimeAction[];
  afterSubmit?: RuntimeAction[];
  onRouteChanged?: RuntimeAction[];
  onPageLeave?: RuntimeAction[];
  onError?: RuntimeAction[];
}
