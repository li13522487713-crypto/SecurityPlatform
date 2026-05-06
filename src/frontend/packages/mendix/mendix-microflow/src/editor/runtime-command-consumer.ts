import type { TestRunMicroflowResponse } from "../runtime-adapter";

export type MicroflowRuntimeCommand = NonNullable<TestRunMicroflowResponse["runtimeCommands"]>[number];

export type RuntimeCommandSeverity = "info" | "warning" | "error";

export interface RuntimeCommandConsumptionResult {
  handled: boolean;
  severity: RuntimeCommandSeverity;
  message: string;
  action: "showPage" | "openTaskPage" | "downloadFile" | "closePage" | "unsupported";
  target?: string;
}

export interface RuntimeCommandConsoleEntry {
  id: string;
  runId?: string;
  commandKind: string;
  handled: boolean;
  message: string;
  severity: RuntimeCommandSeverity;
  target?: string;
  sourceObjectId?: string;
  sourceActionId?: string;
  timestamp: string;
}

interface RuntimeCommandEffects {
  openPage: (target: string) => boolean;
  downloadFile: (url: string, fileName?: string, openInBrowser?: boolean) => boolean;
  closePage: () => boolean;
}

export function isSupportedClientRuntimeCommand(commandKind: string): boolean {
  return commandKind === "showPage"
    || commandKind === "openTaskPage"
    || commandKind === "downloadFile"
    || commandKind === "closePage";
}

export function parseRuntimeCommandPayload(payloadJson?: string): Record<string, unknown> | undefined {
  if (!payloadJson) {
    return undefined;
  }
  try {
    const parsed = JSON.parse(payloadJson);
    return parsed && typeof parsed === "object" ? parsed as Record<string, unknown> : undefined;
  } catch {
    return undefined;
  }
}

function readString(payload: Record<string, unknown> | undefined, keys: string[]): string | undefined {
  if (!payload) {
    return undefined;
  }
  for (const key of keys) {
    const value = payload[key];
    if (typeof value === "string" && value.trim().length > 0) {
      return value.trim();
    }
  }
  return undefined;
}

function isSafeTarget(target: string): boolean {
  const lower = target.trim().toLowerCase();
  return !(lower.startsWith("javascript:") || lower.startsWith("data:"));
}

function buildDefaultEffects(): RuntimeCommandEffects {
  return {
    openPage: (target) => {
      if (typeof window === "undefined" || !isSafeTarget(target)) {
        return false;
      }
      try {
        const opened = window.open(target, "_blank", "noopener,noreferrer");
        return opened !== null;
      } catch {
        return false;
      }
    },
    downloadFile: (url, fileName, openInBrowser) => {
      if (typeof document === "undefined" || !isSafeTarget(url)) {
        return false;
      }
      try {
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.style.display = "none";
        anchor.rel = "noopener noreferrer";
        if (!openInBrowser) {
          anchor.download = fileName ?? "";
        } else {
          anchor.target = "_blank";
        }
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        return true;
      } catch {
        return false;
      }
    },
    closePage: () => {
      if (typeof window === "undefined") {
        return false;
      }
      try {
        if (window.opener) {
          window.close();
          return true;
        }
      } catch {
        return false;
      }
      return false;
    },
  };
}

function normalizeCommandTarget(commandKind: string, payload: Record<string, unknown> | undefined): string | undefined {
  if (commandKind === "showPage") {
    const explicit = readString(payload, ["url", "pageUrl", "pagePath"]);
    if (explicit) {
      return explicit;
    }
    const pageId = readString(payload, ["pageId", "pageRef", "targetPage"]);
    return pageId ? `/pages/${encodeURIComponent(pageId)}` : undefined;
  }
  if (commandKind === "openTaskPage") {
    const explicit = readString(payload, ["url", "taskUrl"]);
    if (explicit) {
      return explicit;
    }
    const taskId = readString(payload, ["workflowTaskId", "taskId"]);
    return taskId ? `/tasks/${encodeURIComponent(taskId)}` : undefined;
  }
  if (commandKind === "downloadFile") {
    return readString(payload, ["downloadUrl", "url", "fileUrl", "href"]);
  }
  return undefined;
}

export function consumeRuntimeCommand(
  command: MicroflowRuntimeCommand,
  payload: Record<string, unknown> | undefined,
  effects?: Partial<RuntimeCommandEffects>,
): RuntimeCommandConsumptionResult {
  const mergedEffects: RuntimeCommandEffects = { ...buildDefaultEffects(), ...effects };

  if (command.commandKind === "showPage" || command.commandKind === "openTaskPage") {
    const target = normalizeCommandTarget(command.commandKind, payload);
    if (!target) {
      return {
        handled: false,
        severity: "warning",
        message: `${command.commandKind} 缺少可导航目标，已保留为宿主处理事件。`,
        action: command.commandKind,
      };
    }
    const opened = mergedEffects.openPage(target);
    return {
      handled: opened,
      severity: opened ? "info" : "warning",
      message: opened
        ? `${command.commandKind} 已打开：${target}`
        : `${command.commandKind} 目标已解析，但当前环境未能直接打开：${target}`,
      action: command.commandKind,
      target,
    };
  }

  if (command.commandKind === "downloadFile") {
    const target = normalizeCommandTarget("downloadFile", payload);
    if (!target) {
      return {
        handled: false,
        severity: "warning",
        message: "downloadFile 缺少下载地址，已保留为宿主处理事件。",
        action: "downloadFile",
      };
    }
    const fileName = readString(payload, ["fileName", "downloadName"]);
    const openInBrowser = payload?.showFileInBrowser === true;
    const downloaded = mergedEffects.downloadFile(target, fileName, openInBrowser);
    return {
      handled: downloaded,
      severity: downloaded ? "info" : "warning",
      message: downloaded
        ? `downloadFile 已触发：${fileName ?? target}`
        : `downloadFile 地址已解析，但当前环境未能触发下载：${target}`,
      action: "downloadFile",
      target,
    };
  }

  if (command.commandKind === "closePage") {
    const closed = mergedEffects.closePage();
    return {
      handled: closed,
      severity: closed ? "info" : "warning",
      message: closed
        ? "closePage 已触发窗口关闭。"
        : "closePage 已接收，但当前窗口不可直接关闭（已回退为宿主事件）。",
      action: "closePage",
    };
  }

  return {
    handled: false,
    severity: "warning",
    message: `未支持的 runtime command：${command.commandKind}`,
    action: "unsupported",
  };
}

export function createRuntimeCommandConsoleEntry(
  command: MicroflowRuntimeCommand,
  result: RuntimeCommandConsumptionResult,
  runId?: string,
  timestamp = new Date().toISOString(),
): RuntimeCommandConsoleEntry {
  return {
    id: `${timestamp}:${runId ?? "run"}:${command.sourceObjectId ?? "object"}:${command.commandKind}`,
    runId,
    commandKind: command.commandKind,
    handled: result.handled,
    message: result.message,
    severity: result.severity,
    target: result.target,
    sourceObjectId: command.sourceObjectId,
    sourceActionId: command.sourceActionId,
    timestamp,
  };
}
