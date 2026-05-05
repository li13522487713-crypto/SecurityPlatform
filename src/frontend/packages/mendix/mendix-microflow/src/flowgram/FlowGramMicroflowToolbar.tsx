import { Button, Space, Tooltip } from "@douyinfe/semi-ui";
import { IconHandle, IconMapPin, IconMinus, IconPlus, IconRedo, IconRefresh, IconTreeTriangleDown, IconUndo } from "@douyinfe/semi-icons";
import { WorkflowResetLayoutService, usePlayground, useService } from "@flowgram-adapter/free-layout-editor";

import { getMendixMicroflowCopy } from "../i18n/copy";

interface MicroflowToolbarViewport {
  x: number;
  y: number;
  zoom: number;
}

interface FlowGramZoomConfig {
  zoom?: number | ((zoom: number) => void);
  updateConfig?: (config: { zoom?: number; scrollX?: number; scrollY?: number }) => void;
}

export interface FlowGramMicroflowToolbarProps {
  readonly?: boolean;
  canUndo?: boolean;
  canRedo?: boolean;
  onUndo?: () => void;
  onRedo?: () => void;
  onAutoLayout?: () => void;
  autoLayoutLoading?: boolean;
  viewport: MicroflowToolbarViewport;
  onViewportChange: (viewport: MicroflowToolbarViewport) => void;
  onFitView?: () => void;
  gridEnabled: boolean;
  onToggleGrid: () => void;
  miniMapVisible: boolean;
  onToggleMiniMap: () => void;
  /** When set, shows the pan-tool toggle wired to Mendix Studio native canvas. */
  panToolActive?: boolean;
  onTogglePanTool?: () => void;
}

export function FlowGramMicroflowToolbar(props: FlowGramMicroflowToolbarProps) {
  const playground = usePlayground();
  const copy = getMendixMicroflowCopy();
  const resetLayout = useService<WorkflowResetLayoutService>(WorkflowResetLayoutService);
  const fitView = () => {
    const service = resetLayout as WorkflowResetLayoutService & { fitView?: () => void };
    service.fitView?.();
    props.onFitView?.();
  };
  const setZoom = (zoom: number) => {
    const normalizedZoom = Math.max(0.2, Math.min(2, zoom));
    const config = playground.config as unknown as FlowGramZoomConfig;
    if (typeof config.zoom === "function") {
      config.zoom(normalizedZoom);
    }
    config.updateConfig?.({
      zoom: normalizedZoom,
      scrollX: props.viewport.x,
      scrollY: props.viewport.y,
    });
    props.onViewportChange({ ...props.viewport, zoom: normalizedZoom });
  };

  return (
    <div className="microflow-flowgram-toolbar">
      <Space>
        {props.onTogglePanTool ? (
          <Tooltip content={copy.canvasToolbar.panToolTooltip}>
            <Button
              icon={<IconHandle />}
              size="small"
              theme={props.panToolActive ? "solid" : "light"}
              aria-label={copy.canvasToolbar.panTool}
              aria-pressed={props.panToolActive === true}
              onClick={props.onTogglePanTool}
            />
          </Tooltip>
        ) : null}
        <Button icon={<IconPlus />} size="small" onClick={() => { playground.config.zoomin(); setZoom(props.viewport.zoom * 1.1); }} />
        <Button icon={<IconMinus />} size="small" onClick={() => { playground.config.zoomout(); setZoom(props.viewport.zoom / 1.1); }} />
        <Button size="small" onClick={() => setZoom(1)}>100%</Button>
        <Button icon={<IconRefresh />} size="small" onClick={fitView} />
        <Button icon={<IconUndo />} size="small" disabled={!props.canUndo} onClick={props.onUndo} />
        <Button icon={<IconRedo />} size="small" disabled={!props.canRedo} onClick={props.onRedo} />
        <Button size="small" theme={props.gridEnabled ? "solid" : "light"} onClick={props.onToggleGrid}>Grid</Button>
        <Button
          icon={<IconMapPin />}
          size="small"
          theme={props.miniMapVisible ? "solid" : "light"}
          onClick={props.onToggleMiniMap}
        />
        <Button
          icon={<IconTreeTriangleDown />}
          size="small"
          loading={props.autoLayoutLoading}
          disabled={props.readonly}
          onClick={() => {
            props.onAutoLayout?.();
            requestAnimationFrame(fitView);
          }}
        >
          Auto
        </Button>
      </Space>
    </div>
  );
}
