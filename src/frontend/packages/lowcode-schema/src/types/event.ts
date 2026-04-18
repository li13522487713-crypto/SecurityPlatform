import type { ActionSchema } from './action';
import type { EventName } from '../shared/enums';

/**
 * EventSchema —— 事件 → 动作链（docx §10.2.5）。
 *
 * 一个 event 可绑定一条 actions 链，链内动作按声明顺序执行，
 * 标记 parallel=true 的相邻动作进入并行批次（PLAN.md §M03 C03-2）。
 */
export interface EventSchema {
  /** 事件名（onClick / onChange / onSubmit / ...）。*/
  name: EventName;
  /** 动作链。*/
  actions: ActionSchema[];
  /** 事件描述（设计期辅助）。*/
  description?: string;
}
