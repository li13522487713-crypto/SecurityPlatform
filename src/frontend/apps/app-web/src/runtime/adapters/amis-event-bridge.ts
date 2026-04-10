/**
 * AMIS 事件桥接层。
 *
 * 将 AMIS 的 onEvent / action / dialog / ajax / reload 事件
 * 转换为平台内部的 RuntimeAction，交给 ActionExecutor 统一执行。
 *
 * 这样 AMIS 仅作为"页面 DSL"，行为编排由平台接管。
 */

import type { RuntimeAction } from "../actions/action-types";
import { executeAction } from "../actions/action-executor";
import type { ActionResult } from "../actions/action-result";

export interface AmisEvent {
  type: string;
  data?: Record<string, unknown>;
}

export interface AmisEventLifecycleHooks {
  beforeSubmit?: () => Promise<void>;
  afterSubmit?: (result: ActionResult) => Promise<void>;
  onSubmitError?: (result: ActionResult) => Promise<void>;
}

/**
 * 将 AMIS 事件转换为 RuntimeAction。
 */
export function mapAmisEventToAction(event: AmisEvent): RuntimeAction | null {
  switch (event.type) {
    case "link":
    case "url": {
      const pageKey = event.data?.["pageKey"] ?? event.data?.["link"];
      if (typeof pageKey === "string") {
        return {
          type: "navigate",
          input: {
            pageKey,
            params: event.data as Record<string, unknown> | undefined,
          },
        };
      }
      return null;
    }
    case "dialog":
    case "drawer": {
      const dialogKey = event.data?.["dialogKey"] ?? (event.data?.["$schema"] ? String(event.type) : undefined);
      return {
        type: "openDialog",
        input: {
          dialogKey: dialogKey ? String(dialogKey) : "",
          payload: event.data,
        },
      };
    }
    case "submit": {
      return {
        type: "submitForm",
        input: {
          formKey: event.data?.["formKey"] as string | undefined,
          validateOnly: false,
        },
      };
    }
    case "ajax": {
      const url = event.data?.["api"] ?? event.data?.["url"];
      if (!url) {
        return null;
      }
      return {
        type: "callApi",
        input: {
          apiKey: String(url),
          method: event.data?.["method"] as "GET" | "POST" | "PUT" | "PATCH" | "DELETE" | undefined,
          body: event.data,
        },
      };
    }
    case "reload": {
      return {
        type: "refresh",
        input: {
          target: event.data?.["target"] as string | undefined,
        },
      };
    }
    case "setValue": {
      const name = event.data?.["name"];
      if (typeof name === "string") {
        return {
          type: "setVar",
          input: { name, value: event.data?.["value"] },
        };
      }
      return null;
    }
    default:
      return null;
  }
}

/**
 * 处理来自 AMIS 的事件。
 * 先映射为 RuntimeAction，再统一执行。
 */
export async function handleAmisEvent(
  event: AmisEvent,
  hooks?: AmisEventLifecycleHooks,
): Promise<ActionResult | null> {
  const action = mapAmisEventToAction(event);
  if (!action) return null;

  if (action.type === "submitForm" && hooks?.beforeSubmit) {
    await hooks.beforeSubmit();
  }

  const result = await executeAction(action);
  if (action.type === "submitForm") {
    if (result.success && hooks?.afterSubmit) {
      await hooks.afterSubmit(result);
    }
    if (!result.success && hooks?.onSubmitError) {
      await hooks.onSubmitError(result);
    }
  }

  return result;
}
