import { IconPlus, IconClose } from "@douyinfe/semi-icons";
import { useMendixStudioStore } from "../store";
import type { StudioWorkbenchTab } from "../store";

function getTabType(tab: StudioWorkbenchTab): { label: string; className: string; explorerNodeId?: string } {
  switch (tab.kind) {
    case "page":
      return {
        label: "P",
        className: "studio-workbench-tab__type-icon--page",
        explorerNodeId: tab.resourceId
      };
    case "microflow":
      return {
        label: "M",
        className: "studio-workbench-tab__type-icon--mf",
        explorerNodeId: tab.microflowId ? `microflow:${tab.microflowId}` : undefined
      };
    case "workflow":
      return {
        label: "W",
        className: "studio-workbench-tab__type-icon--wf",
        explorerNodeId: tab.resourceId
      };
    default:
      return {
        label: "O",
        className: "studio-workbench-tab__type-icon--other",
        explorerNodeId: tab.resourceId
      };
  }
}

export function WorkbenchTabs() {
  const tabs = useMendixStudioStore(state => state.workbenchTabs);
  const activeWorkbenchTabId = useMendixStudioStore(state => state.activeWorkbenchTabId);
  const setActiveWorkbenchTab = useMendixStudioStore(state => state.setActiveWorkbenchTab);
  const closeWorkbenchTab = useMendixStudioStore(state => state.closeWorkbenchTab);
  const setSelectedExplorerNodeId = useMendixStudioStore(state => state.setSelectedExplorerNodeId);

  const handleTabClick = (tab: StudioWorkbenchTab) => {
    const tabType = getTabType(tab);
    setActiveWorkbenchTab(tab.id);
    if (tabType.explorerNodeId) {
      setSelectedExplorerNodeId(tabType.explorerNodeId);
    }
  };

  return (
    <div className="studio-workbench-tabs">
      {tabs.map(tab => {
        const tabType = getTabType(tab);
        const isActive = activeWorkbenchTabId === tab.id;
        return (
          <div
            key={tab.id}
            className={
              "studio-workbench-tab" +
              (isActive ? " studio-workbench-tab--active" : "")
            }
            title={tab.resourceId ?? tab.id}
            onClick={() => handleTabClick(tab)}
          >
            <span className={`studio-workbench-tab__type-icon ${tabType.className}`}>
              {tabType.label}
            </span>
            <span>{tab.title}</span>
            {tab.dirty && <span className="studio-workbench-tab__dirty-dot" />}
            {tab.closable && (
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
                  closeWorkbenchTab(tab.id);
                }}
              >
                <IconClose style={{ fontSize: 10 }} />
              </span>
            )}
          </div>
        );
      })}

      <div className="studio-workbench-tab-add" title="新建 Tab">
        <IconPlus style={{ fontSize: 14 }} />
      </div>
    </div>
  );
}
