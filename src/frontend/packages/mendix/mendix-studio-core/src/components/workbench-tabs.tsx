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
  const saveStateByMicroflowId = useMendixStudioStore(state => state.saveStateByMicroflowId);
  const pendingCloseTabId = useMendixStudioStore(state => state.pendingCloseTabId);
  const tabCloseGuardOpen = useMendixStudioStore(state => state.tabCloseGuardOpen);
  const validationSummaryByMicroflowId = useMendixStudioStore(state => state.validationSummaryByMicroflowId);
  const setActiveWorkbenchTab = useMendixStudioStore(state => state.setActiveWorkbenchTab);
  const closeWorkbenchTab = useMendixStudioStore(state => state.closeWorkbenchTab);
  const cancelWorkbenchTabCloseGuard = useMendixStudioStore(state => state.cancelWorkbenchTabCloseGuard);
  const setSelectedExplorerNodeId = useMendixStudioStore(state => state.setSelectedExplorerNodeId);
  const pendingCloseTab = pendingCloseTabId
    ? tabs.find(tab => tab.id === pendingCloseTabId)
    : undefined;

  const handleTabClick = (tab: StudioWorkbenchTab) => {
    if (activeWorkbenchTabId && activeWorkbenchTabId !== tab.id && dirtyByWorkbenchTabId[activeWorkbenchTabId]) {
      const currentTab = tabs.find(item => item.id === activeWorkbenchTabId);
      Modal.confirm({
        title: "当前微流尚未保存",
        content: `${currentTab?.title ?? "当前微流"} 有未保存更改。当前编辑器尚未持有跨 tab 草稿，切换前请先保存或关闭时选择 Discard。`,
        okText: "留在当前 Tab",
        cancelText: "取消"
      });
      return;
    }
    const tabType = getTabType(tab);
    setActiveWorkbenchTab(tab.id);
    if (tabType.explorerNodeId) {
      setSelectedExplorerNodeId(tabType.explorerNodeId);
    }
  };

  const requestSaveAndClose = () => {
    if (!pendingCloseTab?.microflowId || !pendingCloseTabId) {
      return;
    }
    window.dispatchEvent(new CustomEvent("atlas:microflow-save-request", {
      detail: {
        microflowId: pendingCloseTab.microflowId,
        onSaved: () => closeWorkbenchTab(pendingCloseTabId, { force: true })
      }
    }));
  };

  return (
    <>
      <div className="studio-workbench-tabs">
        {tabs.map(tab => {
          const tabType = getTabType(tab);
          const isActive = activeWorkbenchTabId === tab.id;
          const isDirty = dirtyByWorkbenchTabId[tab.id] || tab.dirty;
          const summary = tab.microflowId ? validationSummaryByMicroflowId[tab.microflowId] : undefined;
          const problemCount = summary ? summary.errorCount || summary.warningCount : 0;
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
              {summary && problemCount > 0 ? (
                <span
                  className="studio-workbench-tab__status"
                  title={`Validation status from last check. ${summary.errorCount} errors, ${summary.warningCount} warnings.`}
                  style={{
                    color: summary.errorCount > 0 ? "var(--semi-color-danger)" : "var(--semi-color-warning)",
                    borderColor: summary.errorCount > 0 ? "var(--semi-color-danger-light-active)" : "var(--semi-color-warning-light-active)",
                    background: summary.errorCount > 0 ? "var(--semi-color-danger-light-default)" : "var(--semi-color-warning-light-default)"
                  }}
                >
                  {summary.errorCount > 0 ? `E${summary.errorCount}` : `W${summary.warningCount}`}
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
            <Button
              type="primary"
              disabled={!pendingCloseTab?.microflowId || Boolean(pendingCloseTab?.microflowId && saveStateByMicroflowId[pendingCloseTab.microflowId]?.saving)}
              loading={Boolean(pendingCloseTab?.microflowId && saveStateByMicroflowId[pendingCloseTab.microflowId]?.saving)}
              onClick={requestSaveAndClose}
            >
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
          {pendingCloseTab?.title ?? "当前文档"} 有未保存更改或仍在保存中。可以 Save 后关闭、Discard 丢弃本地更改并关闭，或 Cancel 保留 tab。
        </Text>
      </Modal>
    </>
  );
}
