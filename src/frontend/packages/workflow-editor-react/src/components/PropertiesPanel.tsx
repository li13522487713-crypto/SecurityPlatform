import { Alert, Button, Collapse, Input, Space } from "antd";
import { CompressOutlined, ExpandOutlined } from "@ant-design/icons";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { validateConfigBySchema } from "../editor/schema-validation";
import type { FormFieldSchema } from "../node-registry";
import type { NodeTemplateMetadata, NodeTypeMetadata, WorkflowNodeTypeKey } from "../types";
import { NodeRegistry, mergeNodeDefaults } from "../node-registry";
import { SchemaForm } from "../form-widgets";

interface PropertiesPanelProps {
  selectedNode: {
    key: string;
    type: WorkflowNodeTypeKey;
    title: string;
    configs: Record<string, unknown>;
  } | null;
  selectedNodeLabel: string;
  template?: NodeTemplateMetadata;
  nodeTypeMeta?: NodeTypeMetadata;
  variableSuggestions?: Array<{ value: string; label?: string }>;
  visible: boolean;
  onChangeNode: (next: { title: string; configs: Record<string, unknown> }) => void;
  onClose: () => void;
}

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

export function PropertiesPanel(props: PropertiesPanelProps) {
  const { t } = useTranslation();
  const [draftTitle, setDraftTitle] = useState("");
  const [draftConfig, setDraftConfig] = useState<Record<string, unknown>>({});
  const [panelWidth, setPanelWidth] = useState(420);
  const [fullscreen, setFullscreen] = useState(false);

  const definition = useMemo(() => {
    if (!props.selectedNode) {
      return null;
    }
    return nodeRegistry.resolve(props.selectedNode.type);
  }, [props.selectedNode]);

  useEffect(() => {
    if (!props.selectedNode || !definition) {
      return;
    }
    setDraftTitle(props.selectedNode.title);
    setDraftConfig(mergeNodeDefaults(definition, props.template, props.selectedNode.configs));
  }, [definition, props.selectedNode, props.template]);

  const errors = useMemo(() => {
    if (!definition || !props.selectedNode) {
      return [];
    }
    return definition.validate?.({ type: props.selectedNode.type, config: draftConfig }) ?? [];
  }, [definition, draftConfig, props.selectedNode]);

  const schemaIssues = useMemo(() => {
    return validateConfigBySchema(draftConfig, props.nodeTypeMeta?.configSchemaJson).issues;
  }, [draftConfig, props.nodeTypeMeta?.configSchemaJson]);

  const allFields = useMemo(() => {
    if (!definition) {
      return [] as FormFieldSchema[];
    }
    return definition.sections.flatMap((section) => section.fields);
  }, [definition]);

  const fieldErrors = useMemo(() => {
    const map: Record<string, string[]> = {};
    for (const issue of schemaIssues) {
      const fieldPath = findBestMatchedFieldPath(issue.path, allFields);
      if (!fieldPath) {
        continue;
      }
      const current = map[fieldPath] ?? [];
      current.push(issue.message);
      map[fieldPath] = current;
    }
    return map;
  }, [allFields, schemaIssues]);

  const panelSchemaErrors = useMemo(() => {
    return schemaIssues
      .filter((issue) => !findBestMatchedFieldPath(issue.path, allFields))
      .map((issue) => `${normalizeIssuePath(issue.path) || "root"}: ${issue.message}`);
  }, [allFields, schemaIssues]);

  const panelErrors = useMemo(() => {
    return [...errors, ...panelSchemaErrors];
  }, [errors, panelSchemaErrors]);

  const items = useMemo(() => {
    if (!props.selectedNode || !definition) {
      return [];
    }
    const basicSections = definition.sections.filter((section) => !section.advanced);
    const advancedSections = definition.sections.filter((section) => section.advanced);

    return [
      {
        key: "basic",
        label: "基础配置",
        children: (
          <div>
            <div className="wf-react-section">
              <div className="wf-react-section-title">{t("wfUi.properties.basic")}</div>
              <div style={{ marginBottom: 8 }}>
                <div className="wf-react-field-label">{t("wfUi.properties.labelTitle")}</div>
                <Input
                  size="small"
                  value={draftTitle}
                  onChange={(event) => {
                    const nextTitle = event.target.value;
                    setDraftTitle(nextTitle);
                    props.onChangeNode({ title: nextTitle, configs: draftConfig });
                  }}
                />
              </div>
              <div style={{ marginBottom: 8 }}>
                <div className="wf-react-field-label">{t("wfUi.properties.labelType")}</div>
                <Input size="small" value={props.selectedNode.type} readOnly />
              </div>
              {props.nodeTypeMeta?.description ? (
                <div style={{ marginBottom: 8 }}>
                  <div className="wf-react-field-label">描述</div>
                  <Input.TextArea rows={2} value={props.nodeTypeMeta.description} readOnly />
                </div>
              ) : null}
            </div>
            <SchemaForm
              sections={basicSections}
              config={draftConfig}
              fieldErrors={fieldErrors}
              variableSuggestions={props.variableSuggestions}
              onChange={(next) => {
                setDraftConfig(next);
                props.onChangeNode({ title: draftTitle, configs: next });
              }}
            />
          </div>
        )
      },
      {
        key: "advanced",
        label: "高级",
        children:
          advancedSections.length > 0 ? (
            <SchemaForm
              sections={advancedSections}
              config={draftConfig}
              fieldErrors={fieldErrors}
              variableSuggestions={props.variableSuggestions}
              onChange={(next) => {
                setDraftConfig(next);
                props.onChangeNode({ title: draftTitle, configs: next });
              }}
            />
          ) : (
            <Input.TextArea rows={4} value={JSON.stringify(draftConfig, null, 2)} readOnly />
          )
      }
    ];
  }, [
    definition,
    draftConfig,
    draftTitle,
    props.nodeTypeMeta?.description,
    props.onChangeNode,
    props.selectedNode,
    t
  ]);

  if (!props.visible || !props.selectedNode) {
    return null;
  }

  return (
    <div
      className={`wf-react-properties-panel${fullscreen ? " wf-react-properties-panel-fullscreen" : ""}`}
      style={fullscreen ? undefined : { width: panelWidth }}
    >
      <div className="wf-react-properties-header">
        <div>
          <div className="wf-react-properties-title">{t("wfUi.properties.title")}</div>
          <div className="wf-react-properties-subtitle">{props.selectedNodeLabel}</div>
        </div>
        <Space size={6}>
          <Button size="small" icon={fullscreen ? <CompressOutlined /> : <ExpandOutlined />} onClick={() => setFullscreen((value) => !value)}>
            {fullscreen ? "退出全屏" : "全屏"}
          </Button>
          <Button size="small" onClick={props.onClose}>
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
      <Collapse size="small" bordered={false} items={items} defaultActiveKey={["basic", "advanced"]} />
      {!fullscreen ? (
        <div
          className="wf-react-properties-resizer"
          onPointerDown={(event) => {
            event.preventDefault();
            const startX = event.clientX;
            const startWidth = panelWidth;
            const move = (pointerEvent: PointerEvent) => {
              const delta = startX - pointerEvent.clientX;
              const next = Math.max(360, Math.min(760, startWidth + delta));
              setPanelWidth(next);
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

