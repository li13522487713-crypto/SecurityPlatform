import { useState, type Ref, type RefObject } from "react";
import { Button, Dropdown, Space, Tag, Toast, Tooltip } from "@douyinfe/semi-ui";
import {
  IconBranch,
  IconCheckCircleStroked,
  IconClock,
  IconMore,
  IconPlay,
  IconRedo,
  IconRefresh,
  IconSave,
  IconSend,
  IconTickCircle,
  IconUndo
} from "@douyinfe/semi-icons";
import type {
  MicroflowEditorHandle,
  MicroflowEditorStatusSnapshot,
  MicroflowToolboxToggleResult,
  MicroflowWorkbenchStatus
} from "@atlas/microflow";
import { useMendixStudioStore } from "../store";
import { MicroflowWorkbenchCommandBus } from "../microflow/workbench/microflow-workbench-command-bus";

export interface MicroflowWorkbenchToolbarProps {
  microflowId: string | undefined;
  editorRef: RefObject<MicroflowEditorHandle | null>;
  status?: MicroflowWorkbenchStatus | null;
  commandBus?: MicroflowWorkbenchCommandBus;
  variant?: "standalone" | "embedded";
  /** Notification for the host to refresh references / impact panels. */
  onViewReferences?: (microflowId: string) => void;
}

export function MicroflowWorkbenchToolbar({
  microflowId,
  editorRef,
  status: controlledStatus,
  commandBus,
  variant = "standalone",
  onViewReferences,
}: MicroflowWorkbenchToolbarProps) {
  // store 拿 dirty / saving 是为了在 ref 还没有就绪（首次渲染、过渡中）时，
  // 工具栏仍能展示一致状态；ref 就绪后 status snapshot 是权威来源。
  const dirtyById = useMendixStudioStore(state => state.dirtyByWorkbenchTabId);
  const saveStateById = useMendixStudioStore(state => state.saveStateByMicroflowId);
  const tabId = microflowId ? `microflow:${microflowId}` : undefined;
  const fallbackDirty = tabId ? Boolean(dirtyById[tabId]) : false;
  const fallbackSaving = microflowId ? Boolean(saveStateById[microflowId]?.saving) : false;
  const fallbackErrorCount = microflowId ? saveStateById[microflowId]?.lastError ? 1 : 0 : 0;

  const [, forceTick] = useState(0);
  const status: MicroflowEditorStatusSnapshot | MicroflowWorkbenchStatus | null = controlledStatus ?? editorRef.current?.getStatus() ?? null;
  const dirty = status?.dirty ?? fallbackDirty;
  const saving = status?.saving ?? fallbackSaving;
  const running = status?.running ?? false;
  const validating = status?.validationStatus === "validating";
  const errorCount = status?.errorCount ?? fallbackErrorCount;
  const warningCount = status?.warningCount ?? 0;
  const nodeElementCount = status?.nodeElementCount ?? 0;
  const recommendedMaxNodeCount = status?.recommendedMaxNodeCount ?? 25;
  const nodeCountLevel = status?.nodeCountLevel ?? "ok";
  const annotationRecommended = status?.annotationRecommended === true;
  const hasAnnotation = status?.hasAnnotation === true;
  const canUndo = status?.canUndo ?? false;
  const canRedo = status?.canRedo ?? false;
  const degradedRunSession = "degradedRunSession" in (status ?? {}) ? Boolean((status as MicroflowWorkbenchStatus).degradedRunSession) : false;
  const sessionHydrated = "sessionHydrated" in (status ?? {}) ? Boolean((status as MicroflowWorkbenchStatus).sessionHydrated) : false;
  const toolboxReady = "toolboxReady" in (status ?? {}) ? Boolean((status as MicroflowWorkbenchStatus).toolboxReady) : true;
  const toolboxLastError = "toolboxLastError" in (status ?? {}) ? (status as MicroflowWorkbenchStatus).toolboxLastError : undefined;
  const nodeCountText = `${nodeElementCount}/${recommendedMaxNodeCount}`;
  const nodeCountColor = nodeCountLevel === "error" ? "red" : nodeCountLevel === "warning" ? "orange" : "green";

  const refreshStatus = () => {
    if (!controlledStatus) {
      forceTick(value => value + 1);
    }
  };
  const callHandle = <T extends keyof MicroflowEditorHandle>(method: T, ...args: Parameters<Extract<MicroflowEditorHandle[T], (...args: never[]) => unknown>>) => {
    const handle = editorRef.current;
    if (!handle) {
      return undefined;
    }
    const fn = handle[method] as unknown as ((...inner: unknown[]) => unknown) | undefined;
    if (typeof fn !== "function") {
      return undefined;
    }
    const result = fn.call(handle, ...(args as unknown[]));
    if (result && typeof (result as Promise<unknown>).then === "function") {
      (result as Promise<unknown>).finally(refreshStatus);
    } else {
      refreshStatus();
    }
    return result;
  };

  const toggleToolboxFromToolbar = () => {
    const result = callHandle("toggleToolbox") as MicroflowToolboxToggleResult | undefined;
    if (!result) {
      Toast.warning("编辑器未就绪，无法打开节点工具箱。");
      return;
    }
    if (result.reason === "focus_mode") {
      Toast.warning("请先退出专注模式，再打开节点工具箱。");
      return;
    }
    if (result.reason === "registry_empty") {
      Toast.error("节点工具箱数据为空，请检查节点注册表。");
      return;
    }
    if (result.reason === "aux_panel_disabled") {
      Toast.error("当前模式禁用了侧边面板，无法打开节点工具箱。");
    }
  };

  const disabled = !microflowId;
  const runCommand = (command: Parameters<MicroflowWorkbenchCommandBus["execute"]>[0], payload?: { panel: "problems" | "debug" | "references" | "info" | "console" }) => {
    if (command !== "microflow.openPanel") {
      switch (command) {
        case "microflow.save":
          callHandle("save");
          return;
        case "microflow.validate":
          callHandle("validate");
          return;
        case "microflow.run":
          callHandle("runTest");
          return;
        case "microflow.debugRun":
          callHandle("runDebug");
          return;
        case "microflow.publish":
          callHandle("publish");
          return;
        case "microflow.undo":
          callHandle("undo");
          return;
        case "microflow.redo":
          callHandle("redo");
          return;
        case "microflow.toggleFocusMode":
          callHandle("toggleFocusMode");
          return;
      }
    }
    if (commandBus) {
      void commandBus.execute(command as never, payload as never).finally(refreshStatus);
      return;
    }
    switch (command) {
      case "microflow.save":
        callHandle("save");
        break;
      case "microflow.validate":
        callHandle("validate");
        break;
      case "microflow.run":
        callHandle("runTest");
        break;
      case "microflow.debugRun":
        callHandle("runDebug");
        break;
      case "microflow.publish":
        callHandle("publish");
        break;
      case "microflow.undo":
        callHandle("undo");
        break;
      case "microflow.redo":
        callHandle("redo");
        break;
      case "microflow.toggleFocusMode":
        if ("toggleFocusMode" in (editorRef.current ?? {})) {
          callHandle("toggleFocusMode");
        } else {
          callHandle("toggleFullscreen");
        }
        break;
      case "microflow.openPanel":
        if (payload?.panel === "references" && microflowId) {
          onViewReferences?.(microflowId);
        } else if (payload?.panel) {
          editorRef.current?.openBottomTab(payload.panel);
          refreshStatus();
        }
        break;
    }
  };

  return (
    <div
      className="studio-workbench-toolbar"
      data-testid="microflow-workbench-toolbar"
      data-microflow-id={microflowId}
      role="toolbar"
      aria-label="Microflow Workbench Toolbar"
      style={{
        display: "flex",
        alignItems: "center",
        gap: 8,
        padding: variant === "embedded" ? "0 12px" : "8px 12px",
        borderBottom: variant === "embedded" ? "none" : "1px solid var(--semi-color-border, #e5e6eb)",
        background: "var(--semi-color-bg-2, #fff)",
        minHeight: variant === "embedded" ? 0 : 48,
        height: variant === "embedded" ? 48 : undefined
      }}
    >
      <Space spacing={6}>
        <Tooltip content={dirty ? "保存（Ctrl+S）" : "无未保存改动"}>
          <Button
            data-testid="microflow-workbench-save"
            theme="light"
            size="small"
            icon={<IconSave />}
            loading={saving}
            disabled={disabled}
            onClick={() => runCommand("microflow.save")}
          >
            保存
          </Button>
        </Tooltip>
      </Space>
      <div style={{ width: 0.5, alignSelf: "stretch", background: "var(--semi-color-border, #e5e6eb)", opacity: 0.5 }} />

      <Space spacing={6}>
        <Tooltip content={errorCount > 0 ? "存在校验错误，请先修复后再运行" : "运行"}>
          <Button
            data-testid="microflow-workbench-run"
            size="small"
            icon={<IconPlay />}
            loading={running}
            disabled={disabled || errorCount > 0}
            style={{ background: "#16a34a", color: "#ffffff", borderColor: "#15803d" }}
            onClick={() => runCommand("microflow.run")}
          >
            运行
          </Button>
        </Tooltip>
        <Tooltip content="调试运行：与运行使用相同 testRun，但运行后默认打开底部 Debug 抽屉以查看 trace 与变量快照。">
          <Button
            data-testid="microflow-workbench-debug-run"
            size="small"
            icon={<IconBranch />}
            loading={running}
            disabled={disabled || errorCount > 0}
            style={{ background: "#f59e0b", color: "#ffffff", borderColor: "#d97706" }}
            onClick={() => runCommand("microflow.debugRun")}
          >
            调试运行
          </Button>
        </Tooltip>
      </Space>
      <div style={{ width: 0.5, alignSelf: "stretch", background: "var(--semi-color-border, #e5e6eb)", opacity: 0.5 }} />

      <Space spacing={6}>
        <Tooltip content="校验">
          <Button
            data-testid="microflow-workbench-validate"
            size="small"
            icon={<IconCheckCircleStroked />}
            loading={validating}
            disabled={disabled}
            onClick={() => runCommand("microflow.validate")}
          >
            校验
          </Button>
        </Tooltip>
        <Tooltip content="将当前微流配置为全节点验收计算图，输出 120。">
          <Button
            data-testid="microflow-workbench-acceptance-120"
            size="small"
            disabled={disabled}
            onClick={() => callHandle("configureAllNodeAcceptance120")}
          >
            <span style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
              <span>验收</span>
              <span style={{ fontSize: 11, lineHeight: "16px", height: 16, padding: "0 6px", borderRadius: 999, border: "1px solid #3b82f6", color: "#2563eb", background: "#eff6ff" }}>120</span>
            </span>
          </Button>
        </Tooltip>
      </Space>
      <div style={{ width: 0.5, alignSelf: "stretch", background: "var(--semi-color-border, #e5e6eb)", opacity: 0.5 }} />

      <Space spacing={6}>
        <Tooltip content={errorCount > 0 ? "存在错误，无法发布" : "发布"}>
          <Button
            data-testid="microflow-workbench-publish"
            size="small"
            icon={<IconSend />}
            disabled={disabled || errorCount > 0 || dirty}
            type="primary"
            onClick={() => runCommand("microflow.publish")}
          >
            发布
          </Button>
        </Tooltip>
      </Space>

      <div style={{ width: 0.5, alignSelf: "stretch", background: "var(--semi-color-border, #e5e6eb)", opacity: 0.5 }} />

      <Space spacing={4}>
        <Tooltip content="撤销 (Ctrl+Z)">
          <Button data-testid="microflow-workbench-undo" size="small" theme="borderless" icon={<IconUndo />} style={{ opacity: disabled || !canUndo ? 0.3 : 1 }} disabled={disabled || !canUndo} onClick={() => runCommand("microflow.undo")} />
        </Tooltip>
        <Tooltip content="重做 (Ctrl+Y)">
          <Button data-testid="microflow-workbench-redo" size="small" theme="borderless" icon={<IconRedo />} style={{ opacity: disabled || !canRedo ? 0.3 : 1 }} disabled={disabled || !canRedo} onClick={() => runCommand("microflow.redo")} />
        </Tooltip>
      </Space>

      <div style={{ flex: 1 }} />

      <Space spacing={6}>
        <Tag
          color={saving ? "blue" : dirty ? "orange" : "green"}
          size="small"
          prefixIcon={saving || dirty ? <IconClock /> : <IconTickCircle />}
          style={{ transition: "all 180ms ease" }}
        >
          {saving ? "保存中" : dirty ? "草稿待保存" : "已保存"}
        </Tag>
        <Tooltip content={annotationRecommended && !hasAnnotation ? "复杂微流建议补充注释说明目的和参数。" : "节点数量建议值 25。"}>
          <Tag color={nodeCountColor} size="small">
            {nodeCountText}
          </Tag>
        </Tooltip>
        {errorCount > 0 ? <Tag color="red" size="small">{errorCount} 错误</Tag> : null}
        {warningCount > 0 ? <Tag color="amber" size="small">{warningCount} 警告</Tag> : null}
        {validating ? <Tag color="blue" size="small" icon={<IconRefresh />}>校验中</Tag> : null}
        {!toolboxReady ? <Tag color="orange" size="small">工具箱未就绪</Tag> : null}
        {toolboxLastError ? <Tag color="red" size="small">工具箱异常</Tag> : null}
        {degradedRunSession ? <Tag color="orange" size="small">会话回读未完成</Tag> : null}
        {!degradedRunSession && sessionHydrated ? <Tag color="green" size="small">会话已收口</Tag> : null}
        {onViewReferences && microflowId ? (
          <Tooltip content="查看引用 / 影响面">
            <Tag
              data-testid="microflow-workbench-references"
              tabIndex={disabled ? -1 : 0}
              style={{ cursor: disabled ? "not-allowed" : "pointer", userSelect: "none", opacity: disabled ? 0.5 : 1 }}
              onClick={() => {
                if (!disabled) {
                  runCommand("microflow.openPanel", { panel: "references" });
                }
              }}
              onKeyDown={event => {
                if (!disabled && (event.key === "Enter" || event.key === " ")) {
                  event.preventDefault();
                  runCommand("microflow.openPanel", { panel: "references" });
                }
              }}
            >
              引用
            </Tag>
          </Tooltip>
        ) : null}
        <Tooltip content="节点工具箱">
          <Tag
            data-testid="microflow-workbench-toolbox"
            tabIndex={disabled ? -1 : 0}
            style={{ cursor: disabled ? "not-allowed" : "pointer", userSelect: "none", opacity: disabled ? 0.5 : 1 }}
            onClick={() => {
              if (!disabled) {
                toggleToolboxFromToolbar();
              }
            }}
            onKeyDown={event => {
              if (!disabled && (event.key === "Enter" || event.key === " ")) {
                event.preventDefault();
                toggleToolboxFromToolbar();
              }
            }}
          >
            节点工具箱
          </Tag>
        </Tooltip>
        <Dropdown
          trigger="click"
          position="bottomRight"
          render={(
            <Dropdown.Menu>
              <Dropdown.Item data-testid="microflow-workbench-more-export-image" disabled={disabled} onClick={() => callHandle("exportAsImage")}>
                导出 PNG
              </Dropdown.Item>
              <Dropdown.Item data-testid="microflow-workbench-more-auto-layout" disabled={disabled} onClick={() => callHandle("autoLayout")}>
                自动排版
              </Dropdown.Item>
              <Dropdown.Item data-testid="microflow-workbench-more-fit-view" disabled={disabled} onClick={() => callHandle("fitView")}>
                适应视图
              </Dropdown.Item>
            </Dropdown.Menu>
          )}
        >
          <Button
            data-testid="microflow-workbench-more"
            size="small"
            theme="borderless"
            icon={<IconMore />}
            disabled={disabled}
            aria-label="更多"
          />
        </Dropdown>
      </Space>
    </div>
  );
}
