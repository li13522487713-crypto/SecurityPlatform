import { Button, Segmented, Select, Space } from "antd";
import { PlusOutlined, BorderOutlined, ZoomInOutlined, PlayCircleOutlined, ApartmentOutlined, AlignCenterOutlined } from "@ant-design/icons";

interface CanvasToolbarProps {
  zoom: number;
  mode: "mouse" | "trackpad";
  minimapVisible: boolean;
  readOnly?: boolean;
  onZoomChange: (value: number) => void;
  onModeChange: (value: "mouse" | "trackpad") => void;
  onToggleNodePanel: () => void;
  onToggleMinimap: () => void;
  onAutoLayout: () => void;
  onToggleVariables: () => void;
  onToggleDebug: () => void;
  onToggleTrace: () => void;
  onToggleProblems: () => void;
  onRun: () => void;
}

const ZOOM_OPTIONS = [50, 75, 100, 125, 150, 200].map((value) => ({
  label: `${value}%`,
  value
}));

export function CanvasToolbar(props: CanvasToolbarProps) {
  const isReadOnly = Boolean(props.readOnly);

  return (
    <div className="wf-react-toolbar">
      <div className="wf-react-toolbar-wrap">
        <div className="wf-react-tools-section">
          <Segmented
            options={[{ label: "鼠标", value: "mouse" }, { label: "手势", value: "trackpad" }]}
            value={props.mode}
            size="small"
            onChange={(value) => props.onModeChange(value as "mouse" | "trackpad")}
          />
          <Select
            size="small"
            value={props.zoom}
            options={ZOOM_OPTIONS}
            style={{ width: 92 }}
            suffixIcon={<ZoomInOutlined />}
            onChange={(value) => props.onZoomChange(Number(value))}
          />
          <Button size="small" type={props.minimapVisible ? "primary" : "default"} icon={<BorderOutlined />} onClick={props.onToggleMinimap}>
            小地图
          </Button>
          {!isReadOnly ? (
            <>
              <Button size="small" icon={<AlignCenterOutlined />} onClick={props.onAutoLayout}>
                自动布局
              </Button>
              <Button type="primary" size="small" icon={<PlusOutlined />} onClick={props.onToggleNodePanel}>
                添加节点
              </Button>
            </>
          ) : null}
        </div>
        <div className="wf-react-tools-section">
          <Space size={8}>
            <Button size="small" icon={<ApartmentOutlined />} onClick={props.onToggleVariables}>
              变量
            </Button>
            <Button size="small" onClick={props.onToggleDebug}>
              单节点调试
            </Button>
            <Button size="small" onClick={props.onToggleTrace}>
              Trace
            </Button>
            <Button size="small" onClick={props.onToggleProblems}>
              问题
            </Button>
            <Button size="small" type="primary" ghost icon={<PlayCircleOutlined />} onClick={props.onRun}>
              测试运行
            </Button>
          </Space>
        </div>
      </div>
    </div>
  );
}

