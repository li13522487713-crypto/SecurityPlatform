import { Select } from "@douyinfe/semi-ui";
import {
  IconUndo,
  IconRedo,
  IconDesktop,
  IconMonitorStroked,
  IconPhone,
  IconPlay,
  IconFullScreenStroked
} from "@douyinfe/semi-icons";
import { useMendixStudioStore } from "../store";

export function WorkbenchToolbar() {
  const setPreviewMode = useMendixStudioStore(state => state.setPreviewMode);
  const activeTab = useMendixStudioStore(state => state.activeTab);

  const isPage = activeTab === "pageBuilder";

  return (
    <div className="studio-workbench-toolbar">
      <div className="studio-workbench-toolbar__left">
        <button className="studio-workbench-toolbar__btn studio-workbench-toolbar__btn--disabled" title="撤销">
          <IconUndo style={{ fontSize: 15 }} />
        </button>
        <button className="studio-workbench-toolbar__btn studio-workbench-toolbar__btn--disabled" title="重做">
          <IconRedo style={{ fontSize: 15 }} />
        </button>

        <div className="studio-workbench-toolbar__divider" />

        {isPage && (
          <>
            <button className="studio-workbench-toolbar__btn" title="桌面端" style={{ color: "var(--studio-blue)" }}>
              <IconDesktop style={{ fontSize: 15 }} />
            </button>
            <button className="studio-workbench-toolbar__btn" title="平板端">
              <IconMonitorStroked style={{ fontSize: 15 }} />
            </button>
            <button className="studio-workbench-toolbar__btn" title="手机端">
              <IconPhone style={{ fontSize: 15 }} />
            </button>

            <div className="studio-workbench-toolbar__divider" />

            <Select
              defaultValue="响应式"
              style={{ width: 88, height: 26 }}
              size="small"
              optionList={[
                { value: "responsive", label: "响应式" },
                { value: "fixed", label: "固定宽度" }
              ]}
            />
            <Select
              defaultValue="1280px"
              style={{ width: 90, height: 26, marginLeft: 4 }}
              size="small"
              optionList={[
                { value: "1920", label: "1920px" },
                { value: "1280", label: "1280px" },
                { value: "1024", label: "1024px" },
                { value: "768", label: "768px" }
              ]}
            />
            <Select
              defaultValue="100%"
              style={{ width: 76, height: 26, marginLeft: 4 }}
              size="small"
              optionList={[
                { value: "150", label: "150%" },
                { value: "125", label: "125%" },
                { value: "100", label: "100%" },
                { value: "75", label: "75%" },
                { value: "50", label: "50%" }
              ]}
            />

            <div className="studio-workbench-toolbar__divider" />

            <button className="studio-workbench-toolbar__btn" title="全屏预览">
              <IconFullScreenStroked style={{ fontSize: 15 }} />
            </button>
          </>
        )}
      </div>

      <button
        className="studio-workbench-toolbar__preview-btn"
        onClick={() => setPreviewMode(true)}
      >
        <IconPlay style={{ fontSize: 14 }} />
        <span>运行预览</span>
      </button>
    </div>
  );
}
