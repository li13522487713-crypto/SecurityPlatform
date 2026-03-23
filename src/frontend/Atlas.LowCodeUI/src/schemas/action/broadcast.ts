/**
 * 【动作 V-5.2 广播事件】
 * broadcast 派发 + onEvent 监听跨组件通信模式
 */
import type { AmisSchema } from "@/types/amis";

/**
 * 创建广播派发动作
 *
 * @description
 * 通过 broadcast actionType 派发自定义事件，其他组件通过 onEvent 监听。
 * 适用于跨组件通信，例如：日期选择器变化 → 图表刷新。
 *
 * @example
 * ```ts
 * // 派发端：日期选择变化时广播
 * broadcastAction({
 *   eventName: 'dateChanged',
 *   data: { startDate: '${startDate}', endDate: '${endDate}' },
 * })
 *
 * // 监听端：图表监听日期变化并刷新
 * onEventListener('dateChanged', [
 *   { actionType: 'reload', componentId: 'salesChart' },
 * ])
 * ```
 */
export function broadcastAction(opts: {
  eventName: string;
  data?: Record<string, unknown>;
  weight?: number;
}): AmisSchema {
  return {
    actionType: "broadcast",
    args: {
      eventName: opts.eventName,
    },
    ...(opts.data ? { data: opts.data } : {}),
    ...(opts.weight ? { weight: opts.weight } : {}),
  };
}

/**
 * 创建事件监听配置
 *
 * @description
 * 生成 onEvent 配置片段，用于挂载到组件上监听广播事件。
 */
export function onEventListener(
  eventName: string,
  actions: AmisSchema[],
): Record<string, unknown> {
  return {
    onEvent: {
      [eventName]: {
        actions,
      },
    },
  };
}

/**
 * 创建带广播功能的按钮
 */
export function broadcastButton(opts: {
  label: string;
  eventName: string;
  data?: Record<string, unknown>;
  level?: string;
  icon?: string;
  className?: string;
}): AmisSchema {
  return {
    type: "button",
    label: opts.label,
    ...(opts.level ? { level: opts.level } : {}),
    ...(opts.icon ? { icon: opts.icon } : {}),
    ...(opts.className ? { className: opts.className } : {}),
    onEvent: {
      click: {
        actions: [broadcastAction({ eventName: opts.eventName, data: opts.data })],
      },
    },
  };
}

/**
 * 创建监听事件并刷新的组件配置片段
 */
export function reloadOnEvent(eventName: string, componentId: string): Record<string, unknown> {
  return onEventListener(eventName, [
    { actionType: "reload", componentId },
  ]);
}

/**
 * 创建监听事件并设置值的组件配置片段
 */
export function setValueOnEvent(eventName: string, componentId: string, value: Record<string, unknown>): Record<string, unknown> {
  return onEventListener(eventName, [
    { actionType: "setValue", componentId, args: { value } },
  ]);
}

// ========== 完整示例：日期联动图表刷新 ==========

/**
 * 日期选择器变化 → 广播 → 图表刷新 的完整示例 Schema
 */
export function dateChartLinkageExample(): AmisSchema {
  return {
    type: "page",
    body: [
      {
        type: "form",
        title: "筛选条件",
        body: [
          {
            type: "input-date-range",
            name: "dateRange",
            label: "日期范围",
            onEvent: {
              change: {
                actions: [
                  broadcastAction({
                    eventName: "dateRangeChanged",
                    data: {
                      startDate: "${event.data.value[0]}",
                      endDate: "${event.data.value[1]}",
                    },
                  }),
                ],
              },
            },
          },
        ],
      },
      {
        type: "chart",
        id: "salesChart",
        api: "/api/v1/charts/sales?start=${startDate}&end=${endDate}",
        height: 400,
        ...onEventListener("dateRangeChanged", [
          { actionType: "reload", componentId: "salesChart" },
        ]),
      },
    ],
  };
}
