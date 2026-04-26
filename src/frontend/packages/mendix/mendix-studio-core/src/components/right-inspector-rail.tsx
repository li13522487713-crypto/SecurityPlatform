import { IconSetting, IconEdit, IconLink, IconBranch } from "@douyinfe/semi-icons";
import { useMendixStudioStore } from "../store";
import type { InspectorTab } from "../store";

interface InspectorItem {
  key: InspectorTab;
  label: string;
  icon: React.ReactNode;
}

const INSPECTOR_ITEMS: InspectorItem[] = [
  { key: "property", label: "属性", icon: <IconSetting style={{ fontSize: 17 }} /> },
  { key: "style", label: "样式", icon: <IconEdit style={{ fontSize: 17 }} /> },
  { key: "binding", label: "绑定", icon: <IconLink style={{ fontSize: 17 }} /> },
  { key: "structure", label: "结构", icon: <IconBranch style={{ fontSize: 17 }} /> }
];

export function RightInspectorRail() {
  const inspectorTab = useMendixStudioStore(state => state.inspectorTab);
  const setInspectorTab = useMendixStudioStore(state => state.setInspectorTab);

  return (
    <div className="studio-inspector-rail">
      {INSPECTOR_ITEMS.map(item => (
        <div
          key={item.key}
          className={
            "studio-inspector-rail__item" +
            (inspectorTab === item.key ? " studio-inspector-rail__item--active" : "")
          }
          onClick={() => setInspectorTab(item.key)}
          title={item.label}
        >
          {item.icon}
          <span className="studio-inspector-rail__label">{item.label}</span>
        </div>
      ))}
    </div>
  );
}
