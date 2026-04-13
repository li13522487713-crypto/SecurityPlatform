import { Input } from "antd";
import { SearchOutlined } from "@ant-design/icons";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import type { WorkflowCategoryKey, WorkflowNodeCatalogItem } from "../constants/node-catalog";
import { WORKFLOW_CATEGORY_ORDER } from "../constants/node-catalog";

interface NodePanelPopoverProps {
  visible: boolean;
  nodes: WorkflowNodeCatalogItem[];
  enabledTypes?: string[];
  onSelect: (nodeType: string) => void;
  onDragStart?: (nodeType: string) => void;
  onDragEnd?: () => void;
}

const CATEGORY_TITLE_KEY: Record<WorkflowCategoryKey, string> = {
  featured: "wfUi.nodePanel.catFeatured",
  logic: "wfUi.nodePanel.catLogic",
  io: "wfUi.nodePanel.catIo",
  memory: "wfUi.nodePanel.catMemory",
  knowledge: "wfUi.nodePanel.catKnowledge",
  database: "wfUi.nodePanel.catDatabase",
  conversation: "wfUi.nodePanel.catConversation"
};

export function buildNodePanelGroups(params: {
  keyword: string;
  nodes: WorkflowNodeCatalogItem[];
  enabledTypes?: string[];
  translate: (key: string) => string;
}): Array<{ category: WorkflowCategoryKey; items: WorkflowNodeCatalogItem[] }> {
  const enabledTypes = params.enabledTypes?.length ? new Set(params.enabledTypes.map((item) => String(item))) : null;
  const availableNodes = enabledTypes ? params.nodes.filter((node) => enabledTypes.has(String(node.type))) : params.nodes;
  const normalized = params.keyword.trim().toLowerCase();

  return WORKFLOW_CATEGORY_ORDER.map((category) => {
    const items = availableNodes.filter((node) => {
      if (node.category !== category) {
        return false;
      }
      if (normalized.length === 0) {
        return true;
      }
      const title = params.translate(node.titleKey).toLowerCase();
      return title.includes(normalized) || node.type.toLowerCase().includes(normalized);
    });
    return { category, items };
  }).filter((group) => group.items.length > 0);
}

export function NodePanelPopover(props: NodePanelPopoverProps) {
  const { t } = useTranslation();
  const [keyword, setKeyword] = useState("");

  const grouped = useMemo(
    () =>
      buildNodePanelGroups({
        keyword,
        nodes: props.nodes,
        enabledTypes: props.enabledTypes,
        translate: t
      }),
    [keyword, props.enabledTypes, props.nodes, t]
  );

  const totalCount = useMemo(() => grouped.reduce((total, item) => total + item.items.length, 0), [grouped]);

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
      <div className="wf-react-node-panel-summary">
        {t("wfUi.nodePanel.resultCount", { count: totalCount })}
      </div>
      <div className="wf-react-node-panel-list">
        {grouped.length === 0 ? <div className="wf-react-node-panel-empty">{t("wfUi.nodePanel.empty")}</div> : null}
        {grouped.map((group: { category: WorkflowCategoryKey; items: WorkflowNodeCatalogItem[] }) => (
          <section key={group.category} className="wf-react-node-group">
            <header className="wf-react-node-group-title">
              <span>{t(CATEGORY_TITLE_KEY[group.category])}</span>
              <span className="wf-react-node-group-count">{group.items.length}</span>
            </header>
            <div className="wf-react-node-grid">
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
                  <span className="wf-react-node-item-body">
                    <span className="wf-react-node-item-title">{t(node.titleKey)}</span>
                    <span className="wf-react-node-item-type">{node.type}</span>
                  </span>
                </button>
              ))}
            </div>
          </section>
        ))}
      </div>
    </div>
  );
}

