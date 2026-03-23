/**
 * 【动作 V-5.3 Ajax/跳转】
 * 7 类动作 Schema 片段：ajax / url / link / toast / confirm / copy / print / download
 */
import type { AmisSchema } from "@/types/amis";

/** 按钮基础选项 */
export interface ActionBaseOptions {
  label: string;
  level?: "primary" | "secondary" | "info" | "success" | "warning" | "danger" | "light" | "dark" | "link" | "default";
  icon?: string;
  size?: "xs" | "sm" | "md" | "lg";
  disabled?: boolean;
  disabledOn?: string;
  visibleOn?: string;
  hiddenOn?: string;
  tooltip?: string;
  confirmText?: string;
  className?: string;
}

function actionBase(opts: ActionBaseOptions, extra: Record<string, unknown>): AmisSchema {
  return {
    type: "button",
    label: opts.label,
    ...(opts.level ? { level: opts.level } : {}),
    ...(opts.icon ? { icon: opts.icon } : {}),
    ...(opts.size ? { size: opts.size } : {}),
    ...(opts.disabled ? { disabled: true } : {}),
    ...(opts.disabledOn ? { disabledOn: opts.disabledOn } : {}),
    ...(opts.visibleOn ? { visibleOn: opts.visibleOn } : {}),
    ...(opts.hiddenOn ? { hiddenOn: opts.hiddenOn } : {}),
    ...(opts.tooltip ? { tooltip: opts.tooltip } : {}),
    ...(opts.confirmText ? { confirmText: opts.confirmText } : {}),
    ...(opts.className ? { className: opts.className } : {}),
    ...extra,
  };
}

/** Ajax 请求动作 */
export function ajaxAction(opts: ActionBaseOptions & {
  api: string | Record<string, unknown>;
  messages?: { success?: string; failed?: string };
  reload?: string;
  redirect?: string;
  feedback?: AmisSchema;
}): AmisSchema {
  return actionBase(opts, {
    actionType: "ajax",
    api: opts.api,
    ...(opts.messages ? { messages: opts.messages } : {}),
    ...(opts.reload ? { reload: opts.reload } : {}),
    ...(opts.redirect ? { redirect: opts.redirect } : {}),
    ...(opts.feedback ? { feedback: opts.feedback } : {}),
  });
}

/** URL 跳转（新标签页或当前页） */
export function urlAction(opts: ActionBaseOptions & {
  url: string;
  blank?: boolean;
}): AmisSchema {
  return actionBase(opts, {
    actionType: "url",
    url: opts.url,
    blank: opts.blank ?? true,
  });
}

/** 页面内路由跳转 */
export function linkAction(opts: ActionBaseOptions & {
  link: string;
}): AmisSchema {
  return actionBase(opts, {
    actionType: "link",
    link: opts.link,
  });
}

/** Toast 提示动作 */
export function toastAction(opts: ActionBaseOptions & {
  toast: {
    items: Array<{
      body: string;
      level?: "info" | "success" | "warning" | "error";
    }>;
    position?: "top-right" | "top-center" | "top-left" | "bottom-right" | "bottom-center" | "bottom-left";
  };
}): AmisSchema {
  return actionBase(opts, {
    actionType: "toast",
    toast: opts.toast,
  });
}

/** 确认对话框动作 */
export function confirmAction(opts: ActionBaseOptions & {
  dialog: AmisSchema;
}): AmisSchema {
  return actionBase(opts, {
    actionType: "dialog",
    dialog: opts.dialog,
  });
}

/** 复制到剪贴板 */
export function copyAction(opts: ActionBaseOptions & {
  content: string;
}): AmisSchema {
  return actionBase(opts, {
    actionType: "copy",
    content: opts.content,
  });
}

/** 下载文件 */
export function downloadAction(opts: ActionBaseOptions & {
  api: string | Record<string, unknown>;
}): AmisSchema {
  return actionBase(opts, {
    actionType: "download",
    api: opts.api,
  });
}

/** 打印（调用浏览器 print） */
export function printAction(opts: ActionBaseOptions & {
  targetName?: string;
} = { label: "打印" }): AmisSchema {
  return actionBase(opts, {
    actionType: "custom",
    onEvent: {
      click: {
        actions: [{
          actionType: "custom",
          script: opts.targetName
            ? `const el = document.querySelector('[name="${opts.targetName}"]'); if(el){ const w = window.open('','_blank'); w.document.write(el.innerHTML); w.document.close(); w.print(); }`
            : "window.print()",
        }],
      },
    },
  });
}
