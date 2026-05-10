import { useState, type Ref, type RefObject } from "react";
import { Button, Space, Tag, Tooltip } from "@douyinfe/semi-ui";
import {
  IconBranch,
  IconCheckCircleStroked,
  IconClock,
  IconFullScreenStroked,
  IconHandle,
  IconPlay,
  IconRedo,
  IconRefresh,
  IconSave,
  IconSend,
  IconUndo
} from "@douyinfe/semi-icons";
import type { MicroflowEditorHandle, MicroflowEditorStatusSnapshot, MicroflowWorkbenchStatus } from "@atlas/microflow";
import { useMendixStudioStore } from "../store";
import { MicroflowWorkbenchCommandBus } from "../microflow/workbench/microflow-workbench-command-bus";

export interface MicroflowWorkbenchToolbarProps {
  microflowId: string | undefined;
  editorRef: RefObject<MicroflowEditorHandle | null>;
  status?: MicroflowWorkbenchStatus | null;
  commandBus?: MicroflowWorkbenchCommandBus;
  /** Notification for the host to refresh references / impact panels. */
  onViewReferences?: (microflowId: string) => void;
}

/**
 * Mendix Studio Workbench 顶部微流工具栏，对齐用户清单 §3.2 完整按钮集：
 *   保存 / 运行 / 调试运行 / 发布 / 撤销 / 重做 / 校验 /
 *   缩放 - / 缩放 + / 适应画布 / 自动布局 / 小地图 / 全屏。
 *
 * 所有按钮都通过 `editorRef.current` 命令式调用 MicroflowEditorHandle；
 * 状态通过 `editorRef.current.getStatus()` 实时拉取并展示在 Tag 与 disabled
 * 上，避免再造一份独立 store。
 */
export function MicroflowWorkbenchToolbar({ microflowId, editorRef, status: controlledStatus, commandBus, onViewReferences }: MicroflowWorkbenchToolbarProps) {
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
  const canUndo = status?.canUndo ?? false;
  const canRedo = status?.canRedo ?? false;
  const fullscreen = status?.fullscreen ?? false;
  const canvasPanToolActive = status?.canvasPanToolActive === true;
  const degradedRunSession = "degradedRunSession" in (status ?? {}) ? Boolean((status as MicroflowWorkbenchStatus).degradedRunSession) : false;
  const sessionHydrated = "sessionHydrated" in (status ?? {}) ? Boolean((status as MicroflowWorkbenchStatus).sessionHydrated) : false;

  const refreshStatus = () => {
    if (!controlledStatus) {
      forceTick(value => value + 1);
    }
  };
  const callHandle = <T extends keyof MicroflowEditorHandle>(method: T, ...args: Parameters<Extract<MicroflowEditorHandle[T], (...args: never[]) => unknown>>) => {
    const handle = editorRef.current;
    if (!handle) {
      return;
    }
    const fn = handle[method] as unknown as ((...inner: unknown[]) => unknown) | undefined;
    if (typeof fn !== "function") {
      return;
    }
    const result = fn.call(handle, ...(args as unknown[]));
    if (result && typeof (result as Promise<unknown>).then === "function") {
      (result as Promise<unknown>).finally(refreshStatus);
    } else {
      refreshStatus();
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
        padding: "6px 12px",
        borderBottom: "1px solid var(--semi-color-border, #e5e6eb)",
        background: "var(--semi-color-bg-2, #fff)",
        minHeight: 40
      }}
    >
      <Space spacing={6}>
        <Tooltip content={dirty ? "保存（Ctrl+S）" : "无未保存改动"}>
          <Button
            data-testid="microflow-workbench-save"
            theme="solid"
            type="primary"
            size="small"
            icon={<IconSave />}
            loading={saving}
            disabled={disabled || !dirty}
            onClick={() => runCommand("microflow.save")}
          >
            保存
          </Button>
        </Tooltip>
        <Tooltip content={errorCount > 0 ? "存在校验错误，请先修复后再运行" : "运行"}>
          <Button
            data-testid="microflow-workbench-run"
            size="small"
            icon={<IconPlay />}
            loading={running}
            disabled={disabled || errorCount > 0}
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
            onClick={() => runCommand("microflow.debugRun")}
          >
            调试运行
          </Button>
        </Tooltip>
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
        {editorRef.current?.configureAllNodeAcceptance120 ? (
          <Tooltip content="将当前微流配置为全节点验收计算图，输出 120。">
            <Button
              data-testid="microflow-workbench-acceptance-120"
              size="small"
              disabled={disabled}
              onClick={() => callHandle("configureAllNodeAcceptance120")}
            >
              验收120
            </Button>
          </Tooltip>
        ) : null}
        <Tooltip content={errorCount > 0 ? "存在错误，无法发布" : "发布"}>
          <Button
            data-testid="microflow-workbench-publish"
            size="small"
            icon={<IconSend />}
            disabled={disabled || errorCount > 0 || dirty}
            onClick={() => runCommand("microflow.publish")}
          >
            发布
          </Button>
        </Tooltip>
      </Space>

      <div style={{ width: 1, height: 20, background: "var(--semi-color-border)" }} />

      <Space spacing={4}>
        <Tooltip content="撤销 (Ctrl+Z)">
          <Button data-testid="microflow-workbench-undo" size="small" theme="borderless" icon={<IconUndo />} disabled={disabled || !canUndo} onClick={() => runCommand("microflow.undo")} />
        </Tooltip>
        <Tooltip content="重做 (Ctrl+Y)">
          <Button data-testid="microflow-workbench-redo" size="small" theme="borderless" icon={<IconRedo />} disabled={disabled || !canRedo} onClick={() => runCommand("microflow.redo")} />
        </Tooltip>
        <Tooltip content="平移画布：开启后在空白处拖移；也可按住空格或鼠标中键拖移。">
          <Button
            data-testid="microflow-workbench-pan-canvas"
            size="small"
            theme={canvasPanToolActive ? "solid" : "borderless"}
            icon={<IconHandle />}
            disabled={disabled}
            aria-label="平移画布"
            aria-pressed={canvasPanToolActive}
            onClick={() => {
              editorRef.current?.togglePanTool?.();
              refreshStatus();
            }}
          />
        </Tooltip>
      </Space>

      <Space spacing={4}>
        <Tooltip content={fullscreen ? "退出专注模式" : "专注模式"}>
          <Button data-testid="microflow-workbench-fullscreen" size="small" theme="borderless" icon={<IconFullScreenStroked />} disabled={disabled} onClick={() => runCommand("microflow.toggleFocusMode")} />
        </Tooltip>
      </Space>

      <div style={{ flex: 1 }} />

      <Space spacing={6}>
        <Tag color={dirty ? "orange" : "green"} size="small" prefixIcon={<IconClock />}>
          {dirty ? "草稿待保存" : saving ? "保存中" : "已保存"}
        </Tag>
        {errorCount > 0 ? <Tag color="red" size="small">{errorCount} 错误</Tag> : null}
        {warningCount > 0 ? <Tag color="amber" size="small">{warningCount} 警告</Tag> : null}
        {validating ? <Tag color="blue" size="small" icon={<IconRefresh />}>校验中</Tag> : null}
        {degradedRunSession ? <Tag color="orange" size="small">会话回读未完成</Tag> : null}
        {!degradedRunSession && sessionHydrated ? <Tag color="green" size="small">会话已收口</Tag> : null}
        {onViewReferences && microflowId ? (
          <Tooltip content="查看引用 / 影响面">
            <Button data-testid="microflow-workbench-references" size="small" theme="borderless" onClick={() => runCommand("microflow.openPanel", { panel: "references" })}>
              引用
            </Button>
          </Tooltip>
        ) : null}
        <Tooltip content="节点工具箱">
          <Button
            data-testid="microflow-workbench-toolbox"
            size="small"
            theme="borderless"
            icon={<IconBranch />}
            disabled={disabled}
            onClick={() => callHandle("toggleToolbox")}
          >
            节点
          </Button>
        </Tooltip>
      </Space>
    </div>
  );
}
