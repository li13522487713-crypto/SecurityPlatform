import { Alert, Button, Collapse, Input, Space } from "antd";
import { CompressOutlined, ExpandOutlined } from "@ant-design/icons";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { validateConfigBySchema } from "../../editor/schema-validation";
import type { FormFieldSchema, FormSectionSchema } from "../../node-registry";
import { NodeRegistry, mergeNodeDefaults, createMetadataBundle } from "../../node-registry";
import { SchemaForm } from "../../form-widgets";
import { useNodeSideSheetStore } from "../../stores/node-side-sheet-store";
import { useWorkflowEditorStore } from "../../stores/workflow-editor-store";
import type { WorkflowModelCatalogItem } from "../../types";

const nodeRegistry = new NodeRegistry();

function normalizeIssuePath(path: string): string {
  return path.replace(/^\.+/, "").trim();
}

function findBestMatchedFieldPath(issuePath: string, fields: FormFieldSchema[]): string | null {
  const normalized = normalizeIssuePath(issuePath);
  if (!normalized) {
    return null;
  }
  const exact = fields.find((field) => field.path === normalized);
  if (exact) {
    return exact.path;
  }
  const prefixMatched = fields
    .map((field) => field.path)
    .filter((fieldPath) => normalized.startsWith(`${fieldPath}.`) || normalized.startsWith(`${fieldPath}[`))
    .sort((a, b) => b.length - a.length);
  return prefixMatched[0] ?? null;
}

function appendCurrentValueOption(
  options: Array<{ label: string; value: string }>,
  currentValue: unknown,
  fallbackLabel?: string
): Array<{ label: string; value: string }> {
  if (typeof currentValue !== "string" || !currentValue.trim()) {
    return options;
  }

  if (options.some((option) => option.value === currentValue)) {
    return options;
  }

  return [...options, { value: currentValue, label: fallbackLabel ?? currentValue }];
}

function buildModelAwareSections(
  nodeType: string,
  sections: FormSectionSchema[],
  currentConfig: Record<string, unknown>,
  modelCatalog: WorkflowModelCatalogItem[]
): FormSectionSchema[] {
  if ((nodeType !== "Llm" && nodeType !== "IntentDetector") || modelCatalog.length === 0) {
    return sections;
  }

  const currentProvider = typeof currentConfig.provider === "string" ? currentConfig.provider.trim() : "";
  const currentModel = typeof currentConfig.model === "string" ? currentConfig.model.trim() : "";

  const providerOptions = appendCurrentValueOption(
    modelCatalog
      .map((item) => ({
        value: item.provider,
        label: `${item.provider} (${item.providerType})`
      }))
      .filter((item, index, list) => list.findIndex((option) => option.value === item.value) === index),
    currentProvider
  );

  const visibleModels = currentProvider
    ? modelCatalog.filter((item) => item.provider === currentProvider)
    : modelCatalog;
  const modelOptions = appendCurrentValueOption(
    visibleModels
      .map((item) => ({
        value: item.model,
        label: currentProvider ? item.label : `${item.label} / ${item.provider}`
      }))
      .filter((item, index, list) => list.findIndex((option) => option.value === item.value && option.label === item.label) === index),
    currentModel
  );

  return sections.map((section) => ({
    ...section,
    fields: section.fields.map((field) => {
      if (field.path === "provider") {
        return {
          ...field,
          kind: "select",
          options: providerOptions
        };
      }

      if (field.path === "model") {
        return {
          ...field,
          kind: "select",
          options: modelOptions
        };
      }

      return field;
    })
  }));
}

export function NodeSideSheet() {
  const { t } = useTranslation();
  const sideSheet = useNodeSideSheetStore();
  const store = useWorkflowEditorStore();
  const selectedNode = useMemo(
    () => store.canvasNodes.find((item) => item.key === sideSheet.currentNodeKey) ?? null,
    [sideSheet.currentNodeKey, store.canvasNodes]
  );
  const metadataBundle = useMemo(
    () => createMetadataBundle(store.nodeTypesMeta, store.nodeTemplates),
    [store.nodeTemplates, store.nodeTypesMeta]
  );

  const definition = useMemo(() => {
    if (!selectedNode) {
      return null;
    }
    return nodeRegistry.resolve(selectedNode.type);
  }, [selectedNode]);

  const currentConfig = selectedNode?.configs ?? {};
  const isReadOnly = store.readOnlyMode;

  const fieldErrors = useMemo(() => {
    const map: Record<string, string[]> = {};
    if (!definition || !selectedNode) {
      return map;
    }
    const issues = validateConfigBySchema(currentConfig, metadataBundle.nodeTypesMap.get(selectedNode.type)?.configSchemaJson).issues;
    const allFields = definition.sections.flatMap((section) => section.fields);
    for (const issue of issues) {
      const fieldPath = findBestMatchedFieldPath(issue.path, allFields);
      if (!fieldPath) {
        continue;
      }
      const current = map[fieldPath] ?? [];
      current.push(issue.message);
      map[fieldPath] = current;
    }
    return map;
  }, [currentConfig, definition, metadataBundle.nodeTypesMap, selectedNode]);

  if (!sideSheet.isVisible || !selectedNode || !definition) {
    return null;
  }

  const selectedNodeLabel = selectedNode.title || t(`wfUi.nodeTypes.${selectedNode.type}`);
  const panelErrors = definition.validate?.({ type: selectedNode.type, config: currentConfig }) ?? [];
  const template = metadataBundle.templatesMap.get(selectedNode.type);
  const sections = buildModelAwareSections(selectedNode.type, definition.sections, currentConfig, store.modelCatalog);

  return (
    <div className={`wf-react-properties-panel${sideSheet.fullscreenPanel ? " wf-react-properties-panel-fullscreen" : ""}`} style={{ width: sideSheet.mainPanelWidth }}>
      <div className="wf-react-properties-header">
        <div>
          <div className="wf-react-properties-title">{t("wfUi.properties.title")}</div>
          <div className="wf-react-properties-subtitle">{selectedNodeLabel}</div>
        </div>
        <Space size={6}>
          <Button
            size="small"
            icon={sideSheet.fullscreenPanel ? <CompressOutlined /> : <ExpandOutlined />}
            onClick={() => sideSheet.setFullscreenPanel(sideSheet.fullscreenPanel ? null : <Input.TextArea rows={12} value={JSON.stringify(currentConfig, null, 2)} readOnly />)}
          >
            {sideSheet.fullscreenPanel ? "退出全屏" : "全屏"}
          </Button>
          <Button size="small" onClick={() => sideSheet.closeSideSheet()}>
            关闭
          </Button>
        </Space>
      </div>
      {panelErrors.length > 0 ? (
        <Alert
          type="warning"
          showIcon
          message="配置校验"
          description={
            <ul style={{ margin: 0, paddingInlineStart: 16 }}>
              {panelErrors.map((error) => (
                <li key={error}>{error}</li>
              ))}
            </ul>
          }
          style={{ marginBottom: 10 }}
        />
      ) : null}
      {sideSheet.fullscreenPanel ? <div style={{ marginBottom: 12 }}>{sideSheet.fullscreenPanel}</div> : null}
      <Collapse
        size="small"
        bordered={false}
        defaultActiveKey={["basic", "advanced"]}
        items={[
          {
            key: "basic",
            label: "基础配置",
            children: (
              <div>
                <div className="wf-react-field-label">{t("wfUi.properties.labelTitle")}</div>
                <Input
                  size="small"
                  disabled={isReadOnly}
                  value={selectedNode.title}
                  onChange={(event) => {
                    const nextTitle = event.target.value;
                    store.setCanvasNodes(store.canvasNodes.map((node) => (node.key === selectedNode.key ? { ...node, title: nextTitle } : node)));
                    store.setDirty(true);
                  }}
                />
                <div style={{ marginTop: 8 }} className="wf-react-field-label">
                  {t("wfUi.properties.labelType")}
                </div>
                <Input size="small" value={selectedNode.type} readOnly />
                <SchemaForm
                  sections={sections.filter((item) => !item.advanced)}
                  config={mergeNodeDefaults(definition, template, currentConfig)}
                  disabled={isReadOnly}
                  fieldErrors={fieldErrors}
                  onChange={(next) => {
                    store.setCanvasNodes(store.canvasNodes.map((node) => (node.key === selectedNode.key ? { ...node, configs: next } : node)));
                    store.setDirty(true);
                  }}
                />
              </div>
            )
          },
          {
            key: "advanced",
            label: "高级",
            children: (
              <SchemaForm
                sections={sections.filter((item) => item.advanced)}
                config={currentConfig}
                disabled={isReadOnly}
                fieldErrors={fieldErrors}
                onChange={(next) => {
                  store.setCanvasNodes(store.canvasNodes.map((node) => (node.key === selectedNode.key ? { ...node, configs: next } : node)));
                  store.setDirty(true);
                }}
              />
            )
          }
        ]}
      />
      {!isReadOnly ? (
        <div
          className="wf-react-properties-resizer"
          onPointerDown={(event) => {
            event.preventDefault();
            const startX = event.clientX;
            const startWidth = sideSheet.mainPanelWidth;
            const move = (pointerEvent: PointerEvent) => {
              const delta = startX - pointerEvent.clientX;
              const next = Math.max(360, Math.min(760, startWidth + delta));
              sideSheet.setMainPanelWidth(next);
            };
            const up = () => {
              window.removeEventListener("pointermove", move);
              window.removeEventListener("pointerup", up);
            };
            window.addEventListener("pointermove", move);
            window.addEventListener("pointerup", up, { once: true });
          }}
        />
      ) : null}
    </div>
  );
}
