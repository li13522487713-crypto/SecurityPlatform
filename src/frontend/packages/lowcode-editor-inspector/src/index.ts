/**
 * @atlas/lowcode-editor-inspector — 检查器（M05 C05-2 / C05-7）。
 *
 * 三 Tab（property / style / events）容器 + 事件配置面板。
 * 本包暴露纯协议：Tab 切换状态、事件链可视化变换工具；React UI 组件由 M07 lowcode-studio-web 渲染。
 */

import { produce } from 'immer';
import type { ActionSchema, ComponentSchema, EventSchema, ResiliencePolicy } from '@atlas/lowcode-schema';

export const INSPECTOR_TABS = ['property', 'style', 'events'] as const;
export type InspectorTab = (typeof INSPECTOR_TABS)[number];

/** 事件配置面板的视图模型。*/
export interface EventEditorViewModel {
  componentId: string;
  events: EventSchema[];
}

export function buildEventEditorVM(component: ComponentSchema): EventEditorViewModel {
  return {
    componentId: component.id,
    events: component.events ?? []
  };
}

/** 在事件链尾部追加动作。*/
export function appendActionToEvent(component: ComponentSchema, eventName: string, action: ActionSchema): ComponentSchema {
  return produce(component, (draft) => {
    const events = draft.events ?? [];
    let evt = events.find((e) => e.name === eventName);
    if (!evt) {
      evt = { name: eventName as EventSchema['name'], actions: [] };
      events.push(evt);
    }
    evt.actions.push(action);
    draft.events = events;
  });
}

export function removeActionAt(component: ComponentSchema, eventName: string, index: number): ComponentSchema {
  return produce(component, (draft) => {
    const evt = draft.events?.find((e) => e.name === eventName);
    if (!evt) return;
    evt.actions.splice(index, 1);
  });
}

export function moveActionAt(component: ComponentSchema, eventName: string, fromIndex: number, toIndex: number): ComponentSchema {
  return produce(component, (draft) => {
    const evt = draft.events?.find((e) => e.name === eventName);
    if (!evt) return;
    const [m] = evt.actions.splice(fromIndex, 1);
    if (m) evt.actions.splice(toIndex, 0, m);
  });
}

/** 设置某动作的 resilience 策略（弹性策略 UI 提交时使用）。*/
export function setActionResilience(component: ComponentSchema, eventName: string, index: number, policy: ResiliencePolicy | undefined): ComponentSchema {
  return produce(component, (draft) => {
    const evt = draft.events?.find((e) => e.name === eventName);
    if (!evt) return;
    const a = evt.actions[index];
    if (!a) return;
    if (policy === undefined) {
      delete a.resilience;
    } else {
      a.resilience = policy;
    }
  });
}

export const __ATLAS_LOWCODE_PACKAGE__ = '@atlas/lowcode-editor-inspector' as const;
