import { useMemo } from "react";
import { Button, Dropdown, Space, Toast } from "@douyinfe/semi-ui";
import {
  IconBell,
  IconBranch,
  IconChevronDown,
  IconHelpCircle,
  IconMore,
  IconPlay,
  IconRedo,
  IconSave,
  IconUndo,
  IconUpload,
  IconVerify
} from "@douyinfe/semi-icons";
import type { MicroflowWorkbenchStatus } from "@atlas/microflow";

import type {
  MicroflowWorkbenchCommandBus,
  MicroflowWorkbenchCommandName,
  MicroflowWorkbenchCommandPayloadMap
} from "../microflow/workbench/microflow-workbench-command-bus";
import { useMendixStudioStore } from "../store";

type StudioHeaderMode = "microflow" | "non-microflow" | "idle";

export interface StudioHeaderProps {
  mode?: StudioHeaderMode;
  commandBus?: MicroflowWorkbenchCommandBus;
  microflowStatus?: MicroflowWorkbenchStatus | null;
  canUndo?: boolean;
  canRedo?: boolean;
  onViewMicroflowReferences?: () => void;
}

export function StudioHeader({
  mode = "idle",
  commandBus,
  microflowStatus,
  canUndo = false,
  canRedo = false,
  onViewMicroflowReferences
}: StudioHeaderProps) {
  const app = useMendixStudioStore(state => state.appSchema);
  const setPreviewMode = useMendixStudioStore(state => state.setPreviewMode);

  const isMicroflow = mode === "microflow";
  const isIdle = mode === "idle";

  const saveDisabled = !microflowStatus || microflowStatus.saving || !microflowStatus.dirty;
  const runDisabled = !microflowStatus || microflowStatus.running || microflowStatus.errorCount > 0;
  const publishDisabled = !microflowStatus || microflowStatus.dirty || microflowStatus.errorCount > 0;
  const validateDisabled = !microflowStatus;
  const undoDisabled = isMicroflow ? !microflowStatus?.canUndo : !canUndo;
  const redoDisabled = isMicroflow ? !microflowStatus?.canRedo : !canRedo;

  const microflowTitle = useMemo(() => {
    if (!microflowStatus?.microflowId) {
      return "Microflow";
    }
    return microflowStatus.microflowId;
  }, [microflowStatus?.microflowId]);

  const runCommand = async <T extends MicroflowWorkbenchCommandName>(
    command: T,
    payload?: MicroflowWorkbenchCommandPayloadMap[T],
    successMessage?: string
  ) => {
    if (!commandBus) {
      return;
    }
    try {
      await commandBus.execute(command, payload);
      if (successMessage) {
        Toast.success({ content: successMessage, duration: 2 });
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      Toast.error({ content: message, duration: 3 });
    }
  };

  const renderMicroflowActions = () => (
    <div style={{ display: "flex", alignItems: "center", gap: 4, flexShrink: 0 }}>
      <Button
        size="small"
        icon={<IconSave />}
        disabled={saveDisabled}
        onClick={() => void runCommand("microflow.save", undefined, "微流已保存")}
      >
        保存
      </Button>
      <Button
        size="small"
        icon={<IconPlay />}
        disabled={runDisabled}
        onClick={() => void runCommand("microflow.run")}
      >
        运行
      </Button>
      <Button
        size="small"
        icon={<IconBranch />}
        disabled={runDisabled}
        onClick={() => void runCommand("microflow.debugRun")}
      >
        调试运行
      </Button>
      <Button
        size="small"
        icon={<IconVerify />}
        disabled={validateDisabled}
        onClick={() => void runCommand("microflow.validate")}
      >
        校验
      </Button>
      <Button
        size="small"
        icon={<IconUpload />}
        disabled={publishDisabled}
        onClick={() => void runCommand("microflow.publish")}
      >
        发布
      </Button>
      <Button
        size="small"
        theme="borderless"
        icon={<IconUndo />}
        disabled={undoDisabled}
        onClick={() => void runCommand("microflow.undo")}
      />
      <Button
        size="small"
        theme="borderless"
        icon={<IconRedo />}
        disabled={redoDisabled}
        onClick={() => void runCommand("microflow.redo")}
      />
      <Dropdown
        trigger="click"
        position="bottomRight"
        render={
          <Dropdown.Menu>
            <Dropdown.Item
              disabled={!microflowStatus}
              onClick={() => {
                if (onViewMicroflowReferences) {
                  onViewMicroflowReferences();
                  return;
                }
                void runCommand("microflow.openPanel", { panel: "references" });
              }}
            >
              查看引用
            </Dropdown.Item>
            <Dropdown.Item disabled={!microflowStatus} onClick={() => void runCommand("microflow.toggleToolbox")}>
              切换节点工具箱
            </Dropdown.Item>
            <Dropdown.Item disabled={!microflowStatus} onClick={() => void runCommand("microflow.autoLayout")}>
              自动排版
            </Dropdown.Item>
            <Dropdown.Item disabled={!microflowStatus} onClick={() => void runCommand("microflow.fitView")}>
              适应视图
            </Dropdown.Item>
            <Dropdown.Item disabled={!microflowStatus} onClick={() => void runCommand("microflow.exportImage")}>
              导出 PNG
            </Dropdown.Item>
            <Dropdown.Item disabled={!microflowStatus} onClick={() => void runCommand("microflow.acceptance120")}>
              验收 120
            </Dropdown.Item>
            <Dropdown.Item disabled={!microflowStatus} onClick={() => void runCommand("microflow.toggleFocusMode")}>
              切换专注模式
            </Dropdown.Item>
            <Dropdown.Item disabled={!microflowStatus} onClick={() => void runCommand("microflow.resetLayout")}>
              恢复默认布局
            </Dropdown.Item>
          </Dropdown.Menu>
        }
      >
        <Button size="small" icon={<IconMore />} />
      </Dropdown>
    </div>
  );

  const renderNonMicroflowActions = () => (
    <div style={{ display: "flex", alignItems: "center", gap: 4, flexShrink: 0 }}>
      <Button size="small" theme="borderless" icon={<IconUndo />} disabled={undoDisabled} />
      <Button size="small" theme="borderless" icon={<IconRedo />} disabled={redoDisabled} />
      <Button size="small" icon={<IconPlay />} onClick={() => setPreviewMode(true)}>
        运行预览
      </Button>
    </div>
  );

  return (
    <div className="studio-header">
      <div style={{ display: "flex", alignItems: "center", gap: 0, flex: 1, minWidth: 0 }}>
        <div className="studio-header__logo">
          <div className="studio-header__mx-badge">mx</div>
          <span className="studio-header__title">Lowcode Studio</span>
        </div>
        <div className="studio-header__divider" />
        <div className="studio-header__app-tag">
          <span style={{ fontSize: 11, opacity: 0.65 }}>应用：</span>
          <span>{app.name || "-"}</span>
          <IconChevronDown size="small" style={{ opacity: 0.65 }} />
        </div>
        {isMicroflow ? (
          <div style={{ marginLeft: 8, color: "rgba(255,255,255,0.72)", fontSize: 12, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
            {microflowTitle}
          </div>
        ) : null}
      </div>

      <Space spacing={4} align="center" style={{ flexShrink: 0 }}>
        {isMicroflow ? renderMicroflowActions() : !isIdle ? renderNonMicroflowActions() : null}
        <Button size="small" theme="borderless" icon={<IconBell />} />
        <Button size="small" theme="borderless" icon={<IconHelpCircle />} />
      </Space>
    </div>
  );
}
