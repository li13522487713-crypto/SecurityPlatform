import { IconChevronDown, IconChevronRight } from "@douyinfe/semi-icons";
import { useMendixStudioStore } from "../store";
import { useState } from "react";

interface StructureNode {
  id: string;
  type: string;
  label: string;
  children?: StructureNode[];
  typeColor?: string;
  typeBg?: string;
}

const STRUCTURE_TREE: StructureNode = {
  id: "widget_root",
  type: "container",
  label: "root",
  typeColor: "#6b7280",
  typeBg: "#f0f2f5",
  children: [
    {
      id: "widget_dataview_main",
      type: "dataView",
      label: "DataView",
      typeColor: "#0958d9",
      typeBg: "#e6f4ff",
      children: [
        { id: "field_requestno", type: "textBox", label: "RequestNo", typeColor: "#6b7280", typeBg: "#f0f2f5" },
        { id: "field_applicant", type: "referenceSelector", label: "Applicant", typeColor: "#6b7280", typeBg: "#f0f2f5" },
        { id: "field_dept", type: "dropDown", label: "Department", typeColor: "#6b7280", typeBg: "#f0f2f5" }
      ]
    },
    { id: "field_amount", type: "numberInput", label: "Amount", typeColor: "#6b7280", typeBg: "#f0f2f5" },
    { id: "field_reason", type: "textArea", label: "Reason", typeColor: "#6b7280", typeBg: "#f0f2f5" },
    { id: "field_status", type: "dropDown", label: "Status", typeColor: "#6b7280", typeBg: "#f0f2f5" },
    { id: "widget_submit_btn", type: "button", label: "Submit", typeColor: "#1677ff", typeBg: "#e6f4ff" },
    { id: "widget_save_btn", type: "button", label: "SaveDraft", typeColor: "#6b7280", typeBg: "#f0f2f5" },
    { id: "widget_cancel_btn", type: "button", label: "Cancel", typeColor: "#6b7280", typeBg: "#f0f2f5" }
  ]
};

interface StructureNodeRowProps {
  node: StructureNode;
  depth: number;
  selectedId: string;
  onSelect: (id: string) => void;
}

function StructureNodeRow({ node, depth, selectedId, onSelect }: StructureNodeRowProps) {
  const [open, setOpen] = useState(depth < 2);
  const hasChildren = (node.children?.length ?? 0) > 0;
  const isSelected = selectedId === node.id;

  return (
    <div>
      <div
        className={"studio-structure-node" + (isSelected ? " studio-structure-node--selected" : "")}
        style={{ paddingLeft: 8 + depth * 14 }}
        onClick={() => {
          if (hasChildren) setOpen(o => !o);
          onSelect(node.id);
        }}
      >
        {hasChildren ? (
          <span style={{ width: 14, flexShrink: 0, color: "#9ca3af", display: "flex", alignItems: "center" }}>
            {open
              ? <IconChevronDown style={{ fontSize: 11 }} />
              : <IconChevronRight style={{ fontSize: 11 }} />}
          </span>
        ) : (
          <span style={{ width: 14, flexShrink: 0 }} />
        )}

        <span
          className="studio-structure-node__type-badge"
          style={{
            background: isSelected ? "rgba(22,119,255,0.12)" : (node.typeBg ?? "#f0f2f5"),
            color: isSelected ? "var(--studio-blue)" : (node.typeColor ?? "#6b7280"),
            marginRight: 4,
            fontSize: 9,
            padding: "1px 4px",
            borderRadius: 3
          }}
        >
          {node.type === "dataView" ? "DV" :
           node.type === "button" ? "Btn" :
           node.type === "textBox" ? "TB" :
           node.type === "textArea" ? "TA" :
           node.type === "numberInput" ? "N" :
           node.type === "dropDown" ? "DD" :
           node.type === "referenceSelector" ? "RS" :
           node.type === "container" ? "C" : node.type.substring(0, 2).toUpperCase()}
        </span>

        <span style={{
          fontSize: 12,
          flex: 1,
          overflow: "hidden",
          textOverflow: "ellipsis",
          whiteSpace: "nowrap",
          color: isSelected ? "var(--studio-blue)" : "var(--studio-text-primary)",
          fontWeight: isSelected ? 600 : 400
        }}>
          {node.label}
        </span>

        <span style={{ fontSize: 10, color: "#9ca3af", flexShrink: 0, marginLeft: 2 }}>
          ({node.type})
        </span>
      </div>

      {open && hasChildren && node.children?.map(child => (
        <StructureNodeRow
          key={child.id}
          node={child}
          depth={depth + 1}
          selectedId={selectedId}
          onSelect={onSelect}
        />
      ))}
    </div>
  );
}

export function WidgetStructurePanel() {
  const selectedWidgetId = useMendixStudioStore(state => state.selectedWidgetId);
  const setSelectedWidgetId = useMendixStudioStore(state => state.setSelectedWidgetId);
  const activeTab = useMendixStudioStore(state => state.activeTab);

  if (activeTab !== "pageBuilder") {
    return (
      <div className="studio-structure">
        <div className="studio-structure__header">组件树</div>
        <div className="studio-structure__body" style={{ display: "flex", alignItems: "center", justifyContent: "center", padding: 20 }}>
          <span style={{ fontSize: 12, color: "#9ca3af" }}>切换到 Page 标签可见组件树</span>
        </div>
      </div>
    );
  }

  return (
    <div className="studio-structure">
      <div className="studio-structure__header">组件树</div>
      <div className="studio-structure__body">
        <StructureNodeRow
          node={STRUCTURE_TREE}
          depth={0}
          selectedId={selectedWidgetId}
          onSelect={id => setSelectedWidgetId(id)}
        />
      </div>
    </div>
  );
}
