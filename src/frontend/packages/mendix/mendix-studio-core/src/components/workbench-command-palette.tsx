import { useMemo, useState } from "react";
import { Button, Empty, Input, Modal, Space, Tag, Typography } from "@douyinfe/semi-ui";

import type { MicroflowWorkbenchCommandBus } from "../microflow/workbench/microflow-workbench-command-bus";
import type { MicroflowWorkbenchStatus } from "@atlas/microflow";

const { Text } = Typography;

interface WorkbenchCommandPaletteProps {
  visible: boolean;
  status?: MicroflowWorkbenchStatus | null;
  commandBus: MicroflowWorkbenchCommandBus;
  onClose: () => void;
}

interface WorkbenchPaletteCommand {
  id: string;
  label: string;
  hint?: string;
  disabled?: boolean;
  run: () => void | Promise<void>;
}

export function WorkbenchCommandPalette({ visible, status, commandBus, onClose }: WorkbenchCommandPaletteProps) {
  const [query, setQuery] = useState("");

  const commands = useMemo<WorkbenchPaletteCommand[]>(() => [
    {
      id: "save",
      label: "Save",
      hint: "Ctrl/Cmd+S",
      disabled: !status?.dirty,
      run: () => commandBus.execute("microflow.save"),
    },
    {
      id: "validate",
      label: "Validate",
      run: () => commandBus.execute("microflow.validate"),
    },
    {
      id: "run",
      label: "Run",
      disabled: Boolean(status?.running || (status?.errorCount ?? 0) > 0),
      run: () => commandBus.execute("microflow.run"),
    },
    {
      id: "debug-run",
      label: "Debug Run",
      disabled: Boolean(status?.running || (status?.errorCount ?? 0) > 0),
      run: () => commandBus.execute("microflow.debugRun"),
    },
    {
      id: "publish",
      label: "Publish",
      disabled: Boolean(status?.dirty || (status?.errorCount ?? 0) > 0),
      run: () => commandBus.execute("microflow.publish"),
    },
    {
      id: "open-problems",
      label: "Open Problems",
      run: () => commandBus.execute("microflow.openPanel", { panel: "problems" }),
    },
    {
      id: "open-debug",
      label: "Open Debug",
      run: () => commandBus.execute("microflow.openPanel", { panel: "debug" }),
    },
    {
      id: "open-console",
      label: "Open Console",
      run: () => commandBus.execute("microflow.openPanel", { panel: "console" }),
    },
    {
      id: "open-info",
      label: "Open Info",
      run: () => commandBus.execute("microflow.openPanel", { panel: "info" }),
    },
    {
      id: "open-references",
      label: "Open References",
      run: () => commandBus.execute("microflow.openPanel", { panel: "references" }),
    },
    {
      id: "toggle-focus",
      label: "Toggle Focus Mode",
      hint: "F11",
      run: () => commandBus.execute("microflow.toggleFocusMode"),
    },
    {
      id: "reset-layout",
      label: "Reset Layout",
      run: () => commandBus.execute("microflow.resetLayout"),
    },
    {
      id: "undo",
      label: "Undo",
      disabled: !status?.canUndo,
      run: () => commandBus.execute("microflow.undo"),
    },
    {
      id: "redo",
      label: "Redo",
      disabled: !status?.canRedo,
      run: () => commandBus.execute("microflow.redo"),
    },
  ], [commandBus, status?.canRedo, status?.canUndo, status?.dirty, status?.errorCount, status?.running]);

  const normalized = query.trim().toLocaleLowerCase();
  const filtered = normalized
    ? commands.filter(command => command.label.toLocaleLowerCase().includes(normalized) || command.id.includes(normalized))
    : commands;

  return (
    <Modal
      visible={visible}
      title="Workbench Command Palette"
      footer={null}
      onCancel={onClose}
      style={{ maxWidth: 560 }}
      bodyStyle={{ paddingTop: 8 }}
      destroyOnClose
    >
      <Space vertical align="start" spacing={10} style={{ width: "100%" }}>
        <Input
          autoFocus
          value={query}
          placeholder="Search commands"
          onChange={setQuery}
          onEnterPress={() => {
            const first = filtered.find(command => !command.disabled);
            if (first) {
              void Promise.resolve(first.run()).finally(onClose);
            }
          }}
        />
        {status ? (
          <Space wrap>
            <Tag color={status.dirty ? "orange" : "green"}>{status.dirty ? "Dirty" : "Saved"}</Tag>
            <Tag color={status.running ? "blue" : "grey"}>{status.running ? "Running" : "Idle"}</Tag>
            <Tag color={(status.errorCount ?? 0) > 0 ? "red" : "green"}>{status.errorCount ?? 0} errors</Tag>
          </Space>
        ) : null}
        <Space vertical align="start" spacing={6} style={{ width: "100%", maxHeight: 360, overflow: "auto" }}>
          {filtered.map(command => (
            <Button
              key={command.id}
              theme="borderless"
              type="tertiary"
              disabled={command.disabled}
              style={{ justifyContent: "space-between", width: "100%" }}
              onClick={() => {
                void Promise.resolve(command.run()).finally(onClose);
              }}
            >
              <Space style={{ width: "100%", justifyContent: "space-between" }}>
                <Text>{command.label}</Text>
                {command.hint ? <Text type="tertiary" size="small">{command.hint}</Text> : null}
              </Space>
            </Button>
          ))}
          {filtered.length === 0 ? <Empty title="No commands" description="Try a different search keyword." /> : null}
        </Space>
      </Space>
    </Modal>
  );
}
