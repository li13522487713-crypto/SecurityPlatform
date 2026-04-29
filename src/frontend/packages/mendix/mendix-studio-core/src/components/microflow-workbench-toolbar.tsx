import { useState, type Ref, type RefObject } from "react";
import { Button, Dropdown, Space, Tag, Tooltip } from "@douyinfe/semi-ui";
import {
  IconBranch,
  IconCheckCircleStroked,
  IconClock,
  IconExpandStroked,
  IconFullScreenStroked,
  IconLayout,
  IconMagnifier,
  IconMinus,
  IconPlay,
  IconPlus,
  IconRedo,
  IconRefresh,
  IconSave,
  IconSend,
  IconUndo
} from "@douyinfe/semi-icons";
import type { MicroflowEditorHandle, MicroflowEditorStatusSnapshot } from "@atlas/microflow";
import { useMendixStudioStore } from "../store";

export interface MicroflowWorkbenchToolbarProps {
  microflowId: string | undefined;
  editorRef: RefObject<MicroflowEditorHandle | null>;
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
export function MicroflowWorkbenchToolbar({ microflowId, editorRef, onViewReferences }: MicroflowWorkbenchToolbarProps) {
  // store 拿 dirty / saving 是为了在 ref 还没有就绪（首次渲染、过渡中）时，
  // 工具栏仍能展示一致状态；ref 就绪后 status snapshot 是权威来源。
  const dirtyById = useMendixStudioStore(state => state.dirtyByWorkbenchTabId);
  const saveStateById = useMendixStudioStore(state => state.saveStateByMicroflowId);
  const tabId = microflowId ? `microflow:${microflowId}` : undefined;
  const fallbackDirty = tabId ? Boolean(dirtyById[tabId]) : false;
  const fallbackSaving = microflowId ? Boolean(saveStateById[microflowId]?.saving) : false;
  const fallbackErrorCount = microflowId ? saveStateById[microflowId]?.lastError ? 1 : 0 : 0;

  const [, forceTick] = useState(0);
  const status: MicroflowEditorStatusSnapshot | null = editorRef.current?.getStatus() ?? null;
  const dirty = status?.dirty ?? fallbackDirty;
  const saving = status?.saving ?? fallbackSaving;
  const running = status?.running ?? false;
  const validating = status?.validationStatus === "validating";
  const errorCount = status?.errorCount ?? fallbackErrorCount;
  const warningCount = status?.warningCount ?? 0;
  const canUndo = status?.canUndo ?? false;
  const canRedo = status?.canRedo ?? false;
  const zoomPercent = status?.zoomPercent ?? 100;
  const fullscreen = status?.fullscreen ?? false;

  const refreshStatus = () => forceTick(value => value + 1);
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

  const handleSetZoom = (value: number) => {
    callHandle("setZoom", value);
  };

  const disabled = !microflowId;

  return (
    <div
      className="studio-workbench-toolbar"
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
            theme="solid"
            type="primary"
            size="small"
            icon={<IconSave />}
            loading={saving}
            disabled={disabled || !dirty}
            onClick={() => callHandle("save")}
          >
            保存
          </Button>
        </Tooltip>
        <Tooltip content={errorCount > 0 ? "存在校验错误，请先修复后再运行" : "运行"}>
          <Button
            size="small"
            icon={<IconPlay />}
            loading={running}
            disabled={disabled || errorCount > 0}
            onClick={() => callHandle("runTest")}
          >
            运行
          </Button>
        </Tooltip>
        <Tooltip content="调试运行：与运行使用相同 testRun，但运行后默认打开底部 Debug 抽屉以查看 trace 与变量快照。">
          <Button
            size="small"
            icon={<IconBranch />}
            loading={running}
            disabled={disabled || errorCount > 0}
            onClick={() => callHandle("runDebug")}
          >
            调试运行
          </Button>
        </Tooltip>
        <Tooltip content="校验">
          <Button
            size="small"
            icon={<IconCheckCircleStroked />}
            loading={validating}
            disabled={disabled}
            onClick={() => callHandle("validate")}
          >
            校验
          </Button>
        </Tooltip>
        <Tooltip content={errorCount > 0 ? "存在错误，无法发布" : "发布"}>
          <Button
            size="small"
            icon={<IconSend />}
            disabled={disabled || errorCount > 0 || dirty}
            onClick={() => callHandle("publish")}
          >
            发布
          </Button>
        </Tooltip>
      </Space>

      <div style={{ width: 1, height: 20, background: "var(--semi-color-border)" }} />

      <Space spacing={4}>
        <Tooltip content="撤销 (Ctrl+Z)">
          <Button size="small" theme="borderless" icon={<IconUndo />} disabled={disabled || !canUndo} onClick={() => callHandle("undo")} />
        </Tooltip>
        <Tooltip content="重做 (Ctrl+Y)">
          <Button size="small" theme="borderless" icon={<IconRedo />} disabled={disabled || !canRedo} onClick={() => callHandle("redo")} />
        </Tooltip>
      </Space>

      <div style={{ width: 1, height: 20, background: "var(--semi-color-border)" }} />

      <Space spacing={4}>
        <Tooltip content="缩小">
          <Button size="small" theme="borderless" icon={<IconMinus />} disabled={disabled} onClick={() => callHandle("zoomOut")} />
        </Tooltip>
        <Dropdown
          trigger="click"
          render={(
            <Dropdown.Menu>
              {[50, 75, 100, 125, 150, 200].map(value => (
                <Dropdown.Item key={value} onClick={() => handleSetZoom(value / 100)}>{value}%</Dropdown.Item>
              ))}
            </Dropdown.Menu>
          )}
        >
          <Button size="small" theme="borderless" disabled={disabled}>{zoomPercent}%</Button>
        </Dropdown>
        <Tooltip content="放大">
          <Button size="small" theme="borderless" icon={<IconPlus />} disabled={disabled} onClick={() => callHandle("zoomIn")} />
        </Tooltip>
        <Tooltip content="适应画布">
          <Button size="small" theme="borderless" icon={<IconMagnifier />} disabled={disabled} onClick={() => callHandle("fitView")} />
        </Tooltip>
        <Tooltip content="自动布局">
          <Button size="small" theme="borderless" icon={<IconLayout />} disabled={disabled} onClick={() => callHandle("autoLayout")} />
        </Tooltip>
        <Tooltip content="小地图开关">
          <Button size="small" theme="borderless" icon={<IconExpandStroked />} disabled={disabled} onClick={() => callHandle("toggleMinimap")} />
        </Tooltip>
        <Tooltip content={fullscreen ? "退出全屏" : "全屏"}>
          <Button size="small" theme="borderless" icon={<IconFullScreenStroked />} disabled={disabled} onClick={() => callHandle("toggleFullscreen")} />
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
        {onViewReferences && microflowId ? (
          <Tooltip content="查看引用 / 影响面">
            <Button size="small" theme="borderless" onClick={() => onViewReferences(microflowId)}>
              引用
            </Button>
          </Tooltip>
        ) : null}
      </Space>
    </div>
  );
}
