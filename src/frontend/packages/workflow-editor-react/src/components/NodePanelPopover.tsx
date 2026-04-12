import { Input, Collapse } from "antd";
import { SearchOutlined } from "@ant-design/icons";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import type { WorkflowCategoryKey, WorkflowNodeCatalogItem } from "../constants/node-catalog";
import { WORKFLOW_CATEGORY_ORDER } from "../constants/node-catalog";

interface NodePanelPopoverProps {
  visible: boolean;
  nodes: WorkflowNodeCatalogItem[];
  onSelect: (nodeType: string) => void;
  onDragStart?: (nodeType: string) => void;
  onDragEnd?: () => void;
}

const CATEGORY_TITLE_KEY: Record<WorkflowCategoryKey, string> = {
  flowControl: "wfUi.nodePanel.catFlowControl",
  ai: "wfUi.nodePanel.catAi",
  dataProcess: "wfUi.nodePanel.catDataProcess",
  external: "wfUi.nodePanel.catExternal",
  knowledge: "wfUi.nodePanel.catKnowledge",
  database: "wfUi.nodePanel.catDatabase",
  conversation: "wfUi.nodePanel.catConversation"
};

export function NodePanelPopover(props: NodePanelPopoverProps) {
  const { t } = useTranslation();
  const [keyword, setKeyword] = useState("");

  const grouped = useMemo(() => {
    const normalized = keyword.trim().toLowerCase();
    return WORKFLOW_CATEGORY_ORDER.map((category) => {
      const items = props.nodes.filter((node) => {
        if (node.category !== category) {
          return false;
        }
        if (normalized.length === 0) {
          return true;
        }
        const title = t(node.titleKey).toLowerCase();
        return title.includes(normalized) || node.type.toLowerCase().includes(normalized);
      });
      return { category, items };
    }).filter((group) => group.items.length > 0);
  }, [keyword, props.nodes, t]);

  if (!props.visible) {
    return null;
  }

  return (
    <div className="wf-react-node-panel" data-testid="workflow.detail.node-panel">
      <div className="wf-react-node-panel-search">
        <Input
          allowClear
          size="small"
          data-testid="workflow.detail.node-panel.search"
          prefix={<SearchOutlined />}
          placeholder={t("wfUi.nodePanel.phSearch")}
          value={keyword}
          onChange={(event) => setKeyword(event.target.value)}
        />
      </div>
      <div className="wf-react-node-panel-list">
        <Collapse
          size="small"
          bordered={false}
          items={grouped.map((group: { category: WorkflowCategoryKey; items: WorkflowNodeCatalogItem[] }) => ({
            key: group.category,
            label: t(CATEGORY_TITLE_KEY[group.category]),
            children: (
              <div className="wf-react-node-items">
                {group.items.map((node: WorkflowNodeCatalogItem) => (
                  <button
                    key={node.type}
                    type="button"
                    className="wf-react-node-item"
                    draggable
                    onClick={() => props.onSelect(node.type)}
                    onDragStart={(event) => {
                      event.dataTransfer.effectAllowed = "copy";
                      event.dataTransfer.setData("text/plain", node.type);
                      event.dataTransfer.setData("application/x-atlas-workflow-node-type", node.type);
                      props.onDragStart?.(node.type);
                    }}
                    onDragEnd={() => props.onDragEnd?.()}
                  >
                    <span className="wf-react-node-item-icon" style={{ backgroundColor: node.color }}>
                      {node.iconText}
                    </span>
                    <span className="wf-react-node-item-title">{t(node.titleKey)}</span>
                  </button>
                ))}
              </div>
            )
          }))}
          defaultActiveKey={WORKFLOW_CATEGORY_ORDER}
        />
      </div>
    </div>
  );
}

