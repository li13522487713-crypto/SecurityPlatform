import type { JsonValue } from '../shared/json';

/**
 * RuntimeStatePatch —— 运行时状态补丁（docx §10.4.3，M03 / M13 落地）。
 *
 * 一个 dispatch 响应可能含多个 patch；按 scope 分发到 page / app / component 状态切片。
 */
export interface RuntimeStatePatch {
  scope: 'page' | 'app' | 'component';
  /** 目标路径（page.formValues / app.currentUser / component.<id>.value）。*/
  path: string;
  op: 'set' | 'merge' | 'unset';
  value?: JsonValue;
  /** 关联组件 ID（用于精准重渲染）。*/
  componentId?: string;
}

/**
 * RuntimeTrace —— dispatch 完整执行链路（docx §10.4.3）。
 *
 * 由 M13 RuntimeEventsController.Dispatch 落地完整 spans。
 */
export interface RuntimeTrace {
  traceId: string;
  appId: string;
  pageId?: string;
  componentId?: string;
  eventName?: string;
  spans: RuntimeSpan[];
  startedAt: string;
  endedAt?: string;
  status: 'running' | 'success' | 'failed';
}

export interface RuntimeSpan {
  spanId: string;
  parentSpanId?: string;
  /** dispatcher.start / action.invoke / workflow.invoke / chatflow.stream / asset.upload / state.patch / error。*/
  name: string;
  /** 状态。*/
  status: 'ok' | 'error';
  attributes?: Record<string, JsonValue>;
  startedAt: string;
  endedAt?: string;
  /** 错误链路（status='error' 时）。*/
  error?: { kind: string; message: string; stack?: string; expressionPath?: string };
}
