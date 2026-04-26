import { IconPlus, IconClose } from "@douyinfe/semi-icons";
import { useMendixStudioStore } from "../store";
import type { MendixStudioTab, ActiveTabId } from "../store";

interface TabDef {
  id: ActiveTabId;
  studioTab: MendixStudioTab;
  label: string;
  typeLabel: string;
  typeClass: string;
  explorerNodeId: string;
}

const TABS: TabDef[] = [
  {
    id: "page",
    studioTab: "pageBuilder",
    label: "PurchaseRequest_EditPage",
    typeLabel: "P",
    typeClass: "studio-workbench-tab__type-icon--page",
    explorerNodeId: "page_purchase_request_edit"
  },
  {
    id: "microflow",
    studioTab: "microflowDesigner",
    label: "MF_SubmitPurchaseRequest",
    typeLabel: "M",
    typeClass: "studio-workbench-tab__type-icon--mf",
    explorerNodeId: "mf_submit_purchase_request"
  },
  {
    id: "workflow",
    studioTab: "workflowDesigner",
    label: "WF_PurchaseApproval",
    typeLabel: "W",
    typeClass: "studio-workbench-tab__type-icon--wf",
    explorerNodeId: "wf_purchase_approval"
  }
];

export function WorkbenchTabs() {
  const activeTabId = useMendixStudioStore(state => state.activeTabId);
  const setActiveTabId = useMendixStudioStore(state => state.setActiveTabId);
  const setActiveTab = useMendixStudioStore(state => state.setActiveTab);
  const setSelectedExplorerNodeId = useMendixStudioStore(state => state.setSelectedExplorerNodeId);

  const handleTabClick = (tab: TabDef) => {
    setActiveTabId(tab.id);
    setActiveTab(tab.studioTab);
    setSelectedExplorerNodeId(tab.explorerNodeId);
  };

  return (
    <div className="studio-workbench-tabs">
      {TABS.map(tab => (
        <div
          key={tab.id}
          className={
            "studio-workbench-tab" +
            (activeTabId === tab.id ? " studio-workbench-tab--active" : "")
          }
          onClick={() => handleTabClick(tab)}
        >
          <span className={`studio-workbench-tab__type-icon ${tab.typeClass}`}>
            {tab.typeLabel}
          </span>
          <span>{tab.label}</span>
          {activeTabId === tab.id && (
            <span
              style={{
                width: 14,
                height: 14,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                borderRadius: 2,
                cursor: "pointer",
                color: "inherit",
                opacity: 0.6
              }}
              onClick={e => {
                e.stopPropagation();
                // Tab 关闭（MVP 阶段不移除，只切换回 page）
                setActiveTabId("page");
                setActiveTab("pageBuilder");
              }}
            >
              <IconClose style={{ fontSize: 10 }} />
            </span>
          )}
        </div>
      ))}

      <div className="studio-workbench-tab-add" title="新建 Tab">
        <IconPlus style={{ fontSize: 14 }} />
      </div>
    </div>
  );
}
