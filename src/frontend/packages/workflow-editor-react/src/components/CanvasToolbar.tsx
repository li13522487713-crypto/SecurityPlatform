import { Button, Segmented, Select, Space } from "antd";
import { PlusOutlined, BorderOutlined, ZoomInOutlined, PlayCircleOutlined, ApartmentOutlined, AlignCenterOutlined } from "@ant-design/icons";
import { useTranslation } from "react-i18next";

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
  const { t } = useTranslation();

  return (
    <div className="wf-react-toolbar">
      <div className="wf-react-toolbar-wrap">
        <div className="wf-react-tools-section">
          <Segmented
            options={[
              { label: t("wfUi.toolbar.mouse"), value: "mouse" },
              { label: t("wfUi.toolbar.trackpad"), value: "trackpad" }
            ]}
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
            {t("wfUi.toolbar.minimap")}
          </Button>
          {!isReadOnly ? (
            <>
              <Button size="small" icon={<AlignCenterOutlined />} onClick={props.onAutoLayout}>
                {t("wfUi.toolbar.autoLayout")}
              </Button>
              <Button
                type="primary"
                size="small"
                icon={<PlusOutlined />}
                onClick={props.onToggleNodePanel}
                data-testid="workflow.detail.toolbar.add-node"
              >
                {t("wfUi.toolbar.addNode")}
              </Button>
            </>
          ) : null}
        </div>
        <div className="wf-react-tools-section">
          <Space size={8}>
            <Button size="small" icon={<ApartmentOutlined />} onClick={props.onToggleVariables} data-testid="workflow.detail.toolbar.variables">
              {t("wfUi.toolbar.variables")}
            </Button>
            <Button size="small" onClick={props.onToggleDebug} data-testid="workflow.detail.toolbar.debug">
              {t("wfUi.toolbar.debug")}
            </Button>
            <Button size="small" onClick={props.onToggleTrace} data-testid="workflow.detail.toolbar.trace">
              {t("wfUi.toolbar.trace")}
            </Button>
            <Button size="small" onClick={props.onToggleProblems}>
              {t("wfUi.toolbar.problems")}
            </Button>
            <Button
              size="small"
              type="primary"
              ghost
              icon={<PlayCircleOutlined />}
              onClick={props.onRun}
              data-testid="workflow.detail.toolbar.test-run"
            >
              {t("wfUi.toolbar.testRun")}
            </Button>
          </Space>
        </div>
      </div>
    </div>
  );
}

