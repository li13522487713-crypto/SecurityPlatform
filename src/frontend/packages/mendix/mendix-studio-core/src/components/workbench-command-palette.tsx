import { useMemo, useState } from "react";
import { Button, Empty, Input, Modal, Space, Tag, Typography } from "@douyinfe/semi-ui";

import type { MicroflowWorkbenchCommandBus } from "../microflow/workbench/microflow-workbench-command-bus";
import type { MicroflowWorkbenchStatus } from "@atlas/microflow";
import type { MicroflowModuleAsset } from "../microflow/resource";
import type { OpenWorkbenchResourceInput, StudioWorkbenchTab } from "../store";
import { getMendixStudioCopy } from "../i18n/copy";

const { Text } = Typography;

interface WorkbenchCommandPaletteProps {
  visible: boolean;
  status?: MicroflowWorkbenchStatus | null;
  commandBus: MicroflowWorkbenchCommandBus;
  modules?: MicroflowModuleAsset[];
  recentTabs?: StudioWorkbenchTab[];
  onOpenResource?: (resource: OpenWorkbenchResourceInput) => void;
  onClose: () => void;
}

interface WorkbenchPaletteCommand {
  id: string;
  label: string;
  hint?: string;
  disabled?: boolean;
  run: () => void | Promise<void>;
}

export function WorkbenchCommandPalette({ visible, status, commandBus, modules = [], recentTabs = [], onOpenResource, onClose }: WorkbenchCommandPaletteProps) {
  const copy = getMendixStudioCopy().commandPalette;
  const [query, setQuery] = useState("");

  const commands = useMemo<WorkbenchPaletteCommand[]>(() => [
    {
      id: "save",
      label: copy.save,
      hint: "Ctrl/Cmd+S",
      disabled: !status?.dirty,
      run: () => commandBus.execute("microflow.save"),
    },
    {
      id: "validate",
      label: copy.validate,
      disabled: !status,
      run: () => commandBus.execute("microflow.validate"),
    },
    {
      id: "run",
      label: copy.run,
      disabled: !status || Boolean(status.running || (status.errorCount ?? 0) > 0),
      run: () => commandBus.execute("microflow.run"),
    },
    {
      id: "debug-run",
      label: copy.debugRun,
      disabled: !status || Boolean(status.running || (status.errorCount ?? 0) > 0),
      run: () => commandBus.execute("microflow.debugRun"),
    },
    {
      id: "publish",
      label: copy.publish,
      disabled: !status || Boolean(status.dirty || (status.errorCount ?? 0) > 0),
      run: () => commandBus.execute("microflow.publish"),
    },
    {
      id: "open-problems",
      label: copy.openProblems,
      disabled: !status,
      run: () => commandBus.execute("microflow.openPanel", { panel: "problems" }),
    },
    {
      id: "open-debug",
      label: copy.openDebug,
      disabled: !status,
      run: () => commandBus.execute("microflow.openPanel", { panel: "debug" }),
    },
    {
      id: "open-console",
      label: copy.openConsole,
      disabled: !status,
      run: () => commandBus.execute("microflow.openPanel", { panel: "console" }),
    },
    {
      id: "open-info",
      label: copy.openInfo,
      disabled: !status,
      run: () => commandBus.execute("microflow.openPanel", { panel: "info" }),
    },
    {
      id: "open-references",
      label: copy.openReferences,
      disabled: !status,
      run: () => commandBus.execute("microflow.openPanel", { panel: "references" }),
    },
    {
      id: "toggle-toolbox",
      label: "切换节点工具箱",
      disabled: !status,
      run: () => commandBus.execute("microflow.toggleToolbox"),
    },
    {
      id: "toggle-focus",
      label: copy.toggleFocusMode,
      hint: "F11",
      disabled: !status,
      run: () => commandBus.execute("microflow.toggleFocusMode"),
    },
    {
      id: "reset-layout",
      label: copy.resetLayout,
      disabled: !status,
      run: () => commandBus.execute("microflow.resetLayout"),
    },
    {
      id: "undo",
      label: copy.undo,
      disabled: !status?.canUndo,
      run: () => commandBus.execute("microflow.undo"),
    },
    {
      id: "redo",
      label: copy.redo,
      disabled: !status?.canRedo,
      run: () => commandBus.execute("microflow.redo"),
    },
  ], [commandBus, copy, status?.canRedo, status?.canUndo, status?.dirty, status?.errorCount, status?.running]);

  const resourceCommands = useMemo<WorkbenchPaletteCommand[]>(() => {
    if (!onOpenResource) {
      return [];
    }
    const resources: WorkbenchPaletteCommand[] = [];
    for (const tab of recentTabs.slice(0, 8)) {
      if (tab.kind === "microflow" || !tab.resourceId) {
        continue;
      }
      resources.push({
        id: `recent-${tab.id}`,
        label: copy.openResource(tab.title),
        hint: copy.recentHint,
        run: () => onOpenResource({
          kind: tab.kind as OpenWorkbenchResourceInput["kind"],
          resourceId: tab.resourceId!,
          moduleId: tab.moduleId,
          title: tab.title,
          qualifiedName: tab.qualifiedName,
          subtitle: tab.subtitle,
        }),
      });
    }
    for (const module of modules) {
      resources.push({
        id: `domain-${module.moduleId}`,
        label: copy.openDomainModel(module.name),
        hint: copy.domainHint,
        run: () => onOpenResource({
          kind: "domainModel",
          resourceId: module.moduleId,
          moduleId: module.moduleId,
          title: copy.openDomainModel(module.name),
          qualifiedName: module.qualifiedName,
        }),
      });
      resources.push({
        id: `security-${module.moduleId}`,
        label: copy.openSecurity(module.name),
        hint: copy.securityHint,
        run: () => onOpenResource({
          kind: "security",
          resourceId: module.moduleId,
          moduleId: module.moduleId,
          title: copy.openSecurity(module.name),
          qualifiedName: module.qualifiedName,
        }),
      });
      for (const page of module.pages ?? []) {
        resources.push({
          id: `page-${page.id}`,
          label: copy.openResource(page.name),
          hint: copy.pageHint,
          run: () => onOpenResource({
            kind: "page",
            resourceId: page.id,
            moduleId: module.moduleId,
            title: page.name || page.qualifiedName,
            qualifiedName: page.qualifiedName,
            subtitle: page.description,
          }),
        });
      }
      for (const workflow of module.workflows ?? []) {
        resources.push({
          id: `workflow-${workflow.id}`,
          label: copy.openResource(workflow.name),
          hint: copy.workflowHint,
          run: () => onOpenResource({
            kind: "workflow",
            resourceId: workflow.id,
            moduleId: module.moduleId,
            title: workflow.name || workflow.qualifiedName,
            qualifiedName: workflow.qualifiedName,
            subtitle: workflow.description,
          }),
        });
      }
    }
    return resources;
  }, [copy, modules, onOpenResource, recentTabs]);

  const normalized = query.trim().toLocaleLowerCase();
  const allCommands = [...commands, ...resourceCommands];
  const filtered = normalized
    ? allCommands.filter(command => command.label.toLocaleLowerCase().includes(normalized) || command.id.includes(normalized) || command.hint?.toLocaleLowerCase().includes(normalized))
    : allCommands;

  return (
    <Modal
      visible={visible}
      title={copy.title}
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
          placeholder={copy.searchPlaceholder}
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
            <Tag color={status.dirty ? "orange" : "green"}>{status.dirty ? copy.dirty : copy.saved}</Tag>
            <Tag color={status.running ? "blue" : "grey"}>{status.running ? copy.running : copy.idle}</Tag>
            <Tag color={(status.errorCount ?? 0) > 0 ? "red" : "green"}>{copy.errors(status.errorCount ?? 0)}</Tag>
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
          {filtered.length === 0 ? <Empty title={copy.noCommandsTitle} description={copy.noCommandsDescription} /> : null}
        </Space>
      </Space>
    </Modal>
  );
}
