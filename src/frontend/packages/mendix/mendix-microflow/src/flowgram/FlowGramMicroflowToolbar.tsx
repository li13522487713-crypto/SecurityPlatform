import { Button, Space, Tooltip } from "@douyinfe/semi-ui";
import { IconHandle, IconMapPin, IconMinus, IconPlus, IconRedo, IconRefresh, IconTreeTriangleDown, IconUndo } from "@douyinfe/semi-icons";
import { WorkflowResetLayoutService, usePlayground, useService } from "@flowgram-adapter/free-layout-editor";

import { getMendixMicroflowCopy } from "../i18n/copy";

interface MicroflowToolbarViewport {
  x: number;
  y: number;
  zoom: number;
}

const MIN_ZOOM = 0.2;
const MAX_ZOOM = 2;

/** Zoom around a point in the canvas container's local coordinates (e.g. viewport center). */
export function microflowZoomViewportAtLocalPoint(
  viewport: MicroflowToolbarViewport,
  localX: number,
  localY: number,
  nextZoom: number,
): MicroflowToolbarViewport {
  const z = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, nextZoom));
  if (Math.abs(z - viewport.zoom) < 1e-6) {
    return { ...viewport, zoom: z };
  }
  const ratio = z / viewport.zoom;
  return {
    x: localX - ratio * (localX - viewport.x),
    y: localY - ratio * (localY - viewport.y),
    zoom: z,
  };
}

export function microflowZoomViewportAtCanvasCenter(
  viewport: MicroflowToolbarViewport,
  containerWidth: number,
  containerHeight: number,
  nextZoom: number,
): MicroflowToolbarViewport {
  if (containerWidth <= 0 || containerHeight <= 0) {
    return { ...viewport, zoom: Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, nextZoom)) };
  }
  return microflowZoomViewportAtLocalPoint(viewport, containerWidth / 2, containerHeight / 2, nextZoom);
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
  /** When set, +/- / 100% zoom keeps the canvas center fixed (viewport scroll adjusted). */
  applyZoomFromCanvasCenter?: (normalizedZoom: number) => void;
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
  const commitViewportZoom = (normalizedZoom: number) => {
    if (props.applyZoomFromCanvasCenter) {
      props.applyZoomFromCanvasCenter(normalizedZoom);
      return;
    }
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

  const setZoom = (zoom: number) => {
    commitViewportZoom(Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, zoom)));
  };

  const bumpZoom = (factor: number) => {
    const normalizedZoom = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, props.viewport.zoom * factor));
    if (!props.applyZoomFromCanvasCenter) {
      if (factor >= 1) {
        playground.config.zoomin();
      } else {
        playground.config.zoomout();
      }
    }
    commitViewportZoom(normalizedZoom);
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
        <Button icon={<IconPlus />} size="small" onClick={() => { bumpZoom(1.1); }} />
        <Button icon={<IconMinus />} size="small" onClick={() => { bumpZoom(1 / 1.1); }} />
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
