import { Alert, Button, Collapse, Input } from "antd";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
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

export function PropertiesPanel(props: PropertiesPanelProps) {
  const { t } = useTranslation();
  const [draftTitle, setDraftTitle] = useState("");
  const [draftConfig, setDraftConfig] = useState<Record<string, unknown>>({});

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
    <div className="wf-react-properties-panel">
      <div className="wf-react-properties-header">
        <div>
          <div className="wf-react-properties-title">{t("wfUi.properties.title")}</div>
          <div className="wf-react-properties-subtitle">{props.selectedNodeLabel}</div>
        </div>
        <Button size="small" onClick={props.onClose}>
          关闭
        </Button>
      </div>
      {errors.length > 0 ? (
        <Alert
          type="warning"
          showIcon
          message="配置校验"
          description={
            <ul style={{ margin: 0, paddingInlineStart: 16 }}>
              {errors.map((error) => (
                <li key={error}>{error}</li>
              ))}
            </ul>
          }
          style={{ marginBottom: 10 }}
        />
      ) : null}
      <Collapse size="small" bordered={false} items={items} defaultActiveKey={["basic", "advanced"]} />
    </div>
  );
}

