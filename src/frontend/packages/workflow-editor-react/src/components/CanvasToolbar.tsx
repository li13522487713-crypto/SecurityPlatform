import { Button, Segmented, Select, Space } from "antd";
import { PlusOutlined, BorderOutlined, ZoomInOutlined, PlayCircleOutlined } from "@ant-design/icons";

interface CanvasToolbarProps {
  zoom: number;
  onZoomChange: (value: number) => void;
  onToggleNodePanel: () => void;
  onRun: () => void;
}

const ZOOM_OPTIONS = [50, 75, 100, 125, 150, 200].map((value) => ({
  label: `${value}%`,
  value
}));

export function CanvasToolbar(props: CanvasToolbarProps) {
  return (
    <div className="wf-react-toolbar">
      <div className="wf-react-toolbar-wrap">
        <div className="wf-react-tools-section">
          <Segmented options={[{ label: "鼠标", value: "mouse" }, { label: "手势", value: "trackpad" }]} defaultValue="mouse" size="small" />
          <Select
            size="small"
            value={props.zoom}
            options={ZOOM_OPTIONS}
            style={{ width: 92 }}
            suffixIcon={<ZoomInOutlined />}
            onChange={(value) => props.onZoomChange(Number(value))}
          />
          <Button size="small" icon={<BorderOutlined />}>
            小地图
          </Button>
          <Button type="primary" size="small" icon={<PlusOutlined />} onClick={props.onToggleNodePanel}>
            添加节点
          </Button>
        </div>
        <div className="wf-react-tools-section">
          <Space size={8}>
            <Button size="small" type="primary" ghost icon={<PlayCircleOutlined />} onClick={props.onRun}>
              测试运行
            </Button>
          </Space>
        </div>
      </div>
    </div>
  );
}

