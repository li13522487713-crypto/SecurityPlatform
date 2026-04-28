import { IconPlus, IconClose } from "@douyinfe/semi-icons";
import { Button, Modal, Space, Typography } from "@douyinfe/semi-ui";
import { useMendixStudioStore } from "../store";
import type { StudioWorkbenchTab } from "../store";

const { Text } = Typography;

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
  const dirtyByWorkbenchTabId = useMendixStudioStore(state => state.dirtyByWorkbenchTabId);
  const pendingCloseTabId = useMendixStudioStore(state => state.pendingCloseTabId);
  const tabCloseGuardOpen = useMendixStudioStore(state => state.tabCloseGuardOpen);
  const setActiveWorkbenchTab = useMendixStudioStore(state => state.setActiveWorkbenchTab);
  const closeWorkbenchTab = useMendixStudioStore(state => state.closeWorkbenchTab);
  const markWorkbenchTabDirty = useMendixStudioStore(state => state.markWorkbenchTabDirty);
  const cancelWorkbenchTabCloseGuard = useMendixStudioStore(state => state.cancelWorkbenchTabCloseGuard);
  const setSelectedExplorerNodeId = useMendixStudioStore(state => state.setSelectedExplorerNodeId);
  const pendingCloseTab = pendingCloseTabId
    ? tabs.find(tab => tab.id === pendingCloseTabId)
    : undefined;

  const handleTabClick = (tab: StudioWorkbenchTab) => {
    if (activeWorkbenchTabId && activeWorkbenchTabId !== tab.id && dirtyByWorkbenchTabId[activeWorkbenchTabId]) {
      const currentTab = tabs.find(item => item.id === activeWorkbenchTabId);
      Modal.confirm({
        title: "切换未保存的微流？",
        content: `${currentTab?.title ?? "当前微流"} 有未保存更改。切换会卸载编辑器并丢弃未保存草稿。`,
        okText: "丢弃并切换",
        cancelText: "取消",
        onOk: () => {
          markWorkbenchTabDirty(activeWorkbenchTabId, false);
          const tabType = getTabType(tab);
          setActiveWorkbenchTab(tab.id);
          if (tabType.explorerNodeId) {
            setSelectedExplorerNodeId(tabType.explorerNodeId);
          }
        }
      });
      return;
    }
    const tabType = getTabType(tab);
    setActiveWorkbenchTab(tab.id);
    if (tabType.explorerNodeId) {
      setSelectedExplorerNodeId(tabType.explorerNodeId);
    }
  };

  return (
    <>
      <div className="studio-workbench-tabs">
        {tabs.map(tab => {
          const tabType = getTabType(tab);
          const isActive = activeWorkbenchTabId === tab.id;
          const isDirty = dirtyByWorkbenchTabId[tab.id] || tab.dirty;
          return (
            <div
              key={tab.id}
              className={
                "studio-workbench-tab" +
                (isActive ? " studio-workbench-tab--active" : "")
              }
              title={tab.qualifiedName ?? tab.subtitle ?? tab.resourceId ?? tab.id}
              onClick={() => handleTabClick(tab)}
              onAuxClick={event => {
                if (event.button === 1 && tab.closable) {
                  event.preventDefault();
                  closeWorkbenchTab(tab.id);
                }
              }}
            >
              <span className={`studio-workbench-tab__type-icon ${tabType.className}`}>
                {tabType.label}
              </span>
              {isDirty ? (
                <span className="studio-workbench-tab__dirty-prefix" aria-label="Unsaved changes">*</span>
              ) : null}
              <span>{tab.title}</span>
              {tab.status ? (
                <span className="studio-workbench-tab__status" title={tab.publishStatus}>
                  {tab.status}
                </span>
              ) : null}
              {isDirty && <span className="studio-workbench-tab__dirty-dot" />}
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

      <Modal
        title="关闭未保存的文档？"
        visible={Boolean(tabCloseGuardOpen && pendingCloseTab)}
        onCancel={cancelWorkbenchTabCloseGuard}
        footer={
          <Space>
            <Button disabled title="Release Stage 05 schema editor integration 后启用保存">
              Save
            </Button>
            <Button onClick={cancelWorkbenchTabCloseGuard}>
              Cancel
            </Button>
            <Button
              type="danger"
              onClick={() => {
                if (pendingCloseTabId) {
                  closeWorkbenchTab(pendingCloseTabId, { force: true });
                }
              }}
            >
              Discard
            </Button>
          </Space>
        }
      >
        <Text>
          {pendingCloseTab?.title ?? "当前文档"} 有未保存更改。Release Stage 05 接入 schema editor 后将启用 Save；本轮可选择 Discard 丢弃 dirty 状态并关闭，或 Cancel 保留 tab。
        </Text>
      </Modal>
    </>
  );
}
