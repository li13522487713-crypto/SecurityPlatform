import { type CSSProperties, useMemo } from "react";
import { Button, Divider, Dropdown, Space, Spin, Tag, Tooltip } from "@douyinfe/semi-ui";
import { IconClock, IconGridRectangle, IconHandle, IconMapPin, IconMinus, IconPlay, IconPlus, IconRedo, IconRefresh, IconStop, IconTreeTriangleDown, IconUndo } from "@douyinfe/semi-icons";
import { WorkflowResetLayoutService, usePlayground, useService } from "@flowgram-adapter/free-layout-editor";

import { getMendixMicroflowCopy } from "../i18n/copy";
import type { MicroflowValidationIssue } from "../schema";
import type { MicroflowComplexitySummary } from "../utils/microflow-validator";

interface MicroflowToolbarViewport {
  x: number;
  y: number;
  zoom: number;
}

const MIN_ZOOM = 0.2;
const MAX_ZOOM = 2;
const ZOOM_STEP = 1.25;

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
    x: ratio * (localX + viewport.x) - localX,
    y: ratio * (localY + viewport.y) - localY,
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
  onCenterView?: () => void;
  gridEnabled: boolean;
  onToggleGrid: () => void;
  miniMapVisible: boolean;
  onToggleMiniMap: () => void;
  /** When set, shows the pan-tool toggle wired to Mendix Studio native canvas. */
  panToolActive?: boolean;
  onTogglePanTool?: () => void;
  /** When set, +/- / 100% zoom keeps the canvas center fixed (viewport scroll adjusted). */
  applyZoomFromCanvasCenter?: (normalizedZoom: number) => void;
  dirty?: boolean;
  saving?: boolean;
  validating?: boolean;
  validationIssues?: MicroflowValidationIssue[];
  onOpenProblemsPanel?: () => void;
  onNavigateToIssue?: (objectId: string) => void;
  microflowComplexity?: MicroflowComplexitySummary;
  onRun?: () => void;
  onStopRun?: () => void;
  isRunning?: boolean;
}

const draftTagStyle: CSSProperties = {
  backgroundColor: "rgba(255, 237, 213, 0.95)",
  color: "#7c2d12",
  border: "1px solid rgba(180, 83, 9, 0.2)",
};
const savedTagStyle: CSSProperties = {
  backgroundColor: "rgba(240, 255, 244, 0.95)",
  color: "#166534",
  border: "1px solid rgba(22, 163, 74, 0.2)",
};
const savingTagStyle: CSSProperties = {
  backgroundColor: "rgba(219, 234, 254, 0.95)",
  color: "#1d4ed8",
  border: "1px solid rgba(59, 130, 246, 0.25)",
};
const errorTagStyle: CSSProperties = {
  backgroundColor: "rgba(254, 226, 226, 0.95)",
  color: "#991b1b",
  border: "1px solid rgba(220, 38, 38, 0.2)",
  cursor: "pointer",
};
const warningTagStyle: CSSProperties = {
  backgroundColor: "rgba(254, 249, 195, 0.95)",
  color: "#854d0e",
  border: "1px solid rgba(202, 138, 4, 0.25)",
  cursor: "pointer",
};
const nodeCountSuccessStyle: CSSProperties = {
  backgroundColor: "#052e16",
  color: "#4ade80",
  border: "1px solid #1a4a2a",
};
const nodeCountWarningStyle: CSSProperties = {
  backgroundColor: "#451a03",
  color: "#f59e0b",
  border: "1px solid #92400e",
};
const nodeCountErrorStyle: CSSProperties = {
  backgroundColor: "#450a0a",
  color: "#f87171",
  border: "1px solid #7f1d1d",
};

export function FlowGramMicroflowToolbar(props: FlowGramMicroflowToolbarProps) {
  const playground = usePlayground();
  const copy = getMendixMicroflowCopy();
  const resetLayout = useService<WorkflowResetLayoutService>(WorkflowResetLayoutService);

  const { errorCount, warningCount, firstErrorObjectId, firstWarningObjectId } = useMemo(() => {
    let errors = 0;
    let warnings = 0;
    let firstErr: string | undefined;
    let firstWarn: string | undefined;
    for (const issue of props.validationIssues ?? []) {
      if (issue.severity === "error") {
        errors += 1;
        if (!firstErr && issue.objectId) firstErr = issue.objectId;
      } else if (issue.severity === "warning") {
        warnings += 1;
        if (!firstWarn && issue.objectId) firstWarn = issue.objectId;
      }
    }
    return { errorCount: errors, warningCount: warnings, firstErrorObjectId: firstErr, firstWarningObjectId: firstWarn };
  }, [props.validationIssues]);

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

  const zoomPercent = `${Math.round(props.viewport.zoom * 100)}%`;
  const microflowNodeCountText = props.microflowComplexity == null
    ? undefined
    : props.microflowComplexity.level === "error"
      ? `✕ ${props.microflowComplexity.totalElements} / ${props.microflowComplexity.recommendedMaxNodes}`
      : props.microflowComplexity.level === "warning"
        ? `⚠ ${props.microflowComplexity.totalElements} / ${props.microflowComplexity.recommendedMaxNodes}`
        : `✓ ${props.microflowComplexity.totalElements}`;
  const microflowNodeCountStyle = props.microflowComplexity == null
    ? undefined
    : props.microflowComplexity.level === "error"
      ? nodeCountErrorStyle
      : props.microflowComplexity.level === "warning"
        ? nodeCountWarningStyle
        : nodeCountSuccessStyle;
  const microflowNodeCountTooltip = props.microflowComplexity?.level === "error"
    ? "建议将部分逻辑提取为子微流"
    : undefined;

  return (
    <div className="microflow-flowgram-toolbar" style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}>
      <Space spacing={2}>
        {/* 平移工具 */}
        {props.onTogglePanTool ? (
          <>
            <Tooltip content={copy.canvasToolbar.panToolTooltip} position="bottom">
              <Button
                icon={<IconHandle />}
                size="small"
                theme={props.panToolActive ? "solid" : "light"}
                aria-label={copy.canvasToolbar.panTool}
                aria-pressed={props.panToolActive === true}
                onClick={props.onTogglePanTool}
              />
            </Tooltip>
            <Divider layout="vertical" style={{ height: 20, margin: "0 2px" }} />
          </>
        ) : null}

        {/* 缩放控制 */}
        <Tooltip content={copy.canvasToolbar.zoomOutTooltip} position="bottom">
          <Button icon={<IconMinus />} size="small" aria-label={copy.canvasToolbar.zoomOut} onClick={() => { bumpZoom(1 / ZOOM_STEP); }} />
        </Tooltip>
        <Dropdown
          trigger="click"
          position="bottom"
          render={
            <Dropdown.Menu>
              {copy.canvasToolbar.zoomLevels.map(level => (
                <Dropdown.Item
                  key={level.value}
                  active={Math.abs(props.viewport.zoom - level.value) < 0.01}
                  onClick={() => setZoom(level.value)}
                >
                  {level.label}
                </Dropdown.Item>
              ))}
            </Dropdown.Menu>
          }
        >
          <Tooltip content={copy.canvasToolbar.zoomResetTooltip} position="bottom">
            <Button size="small" style={{ minWidth: 52 }} aria-label={copy.canvasToolbar.zoomReset}>
              {zoomPercent}
            </Button>
          </Tooltip>
        </Dropdown>
        <Tooltip content={copy.canvasToolbar.zoomInTooltip} position="bottom">
          <Button icon={<IconPlus />} size="small" aria-label={copy.canvasToolbar.zoomIn} onClick={() => { bumpZoom(ZOOM_STEP); }} />
        </Tooltip>
        <Tooltip content={copy.canvasToolbar.fitViewTooltip} position="bottom">
          <Button icon={<IconRefresh />} size="small" aria-label={copy.canvasToolbar.fitView} onClick={fitView} />
        </Tooltip>
        <Tooltip content={copy.canvasToolbar.centerViewTooltip} position="bottom">
          <Button icon={<IconMapPin />} size="small" aria-label={copy.canvasToolbar.centerView} onClick={props.onCenterView} />
        </Tooltip>

        <Divider layout="vertical" style={{ height: 20, margin: "0 2px" }} />

        {/* 视图开关 */}
        <Tooltip content={copy.canvasToolbar.gridTooltip} position="bottom">
          <Button
            icon={<IconGridRectangle />}
            size="small"
            theme={props.gridEnabled ? "solid" : "light"}
            aria-label={copy.canvasToolbar.grid}
            aria-pressed={props.gridEnabled}
            onClick={props.onToggleGrid}
          />
        </Tooltip>
        <Tooltip content={copy.canvasToolbar.minimapTooltip} position="bottom">
          <Button
            icon={<IconMapPin />}
            size="small"
            theme={props.miniMapVisible ? "solid" : "light"}
            aria-label={copy.canvasToolbar.minimap}
            aria-pressed={props.miniMapVisible}
            onClick={props.onToggleMiniMap}
          />
        </Tooltip>

        <Divider layout="vertical" style={{ height: 20, margin: "0 2px" }} />

        {/* 自动排版 */}
        <Tooltip content={copy.canvasToolbar.autoLayoutTooltip} position="bottom">
          <Button
            icon={<IconTreeTriangleDown />}
            size="small"
            loading={props.autoLayoutLoading}
            disabled={props.readonly}
            aria-label={copy.canvasToolbar.autoLayout}
            onClick={() => {
              props.onAutoLayout?.();
              requestAnimationFrame(fitView);
            }}
          >
            {copy.canvasToolbar.autoLayout}
          </Button>
        </Tooltip>
        {microflowNodeCountText ? (
          <>
            <Divider layout="vertical" style={{ height: 20, margin: "0 2px" }} />
            <Tooltip content={microflowNodeCountTooltip} position="bottom">
              <Tag
                size="small"
                style={{
                  ...microflowNodeCountStyle,
                  cursor: props.onOpenProblemsPanel ? "pointer" : "default",
                }}
                onClick={() => props.onOpenProblemsPanel?.()}
              >
                {microflowNodeCountText}
              </Tag>
            </Tooltip>
          </>
        ) : null}
        {errorCount > 0 ? (
          <>
            <Divider layout="vertical" style={{ height: 20, margin: "0 2px" }} />
            <Tooltip content="跳转到第一个错误节点" position="bottom">
              <Tag
                size="small"
                style={{ ...errorTagStyle }}
                onClick={() => {
                  if (firstErrorObjectId) props.onNavigateToIssue?.(firstErrorObjectId);
                  props.onOpenProblemsPanel?.();
                }}
              >
                ✕ {errorCount} 错误
              </Tag>
            </Tooltip>
          </>
        ) : null}
        {warningCount > 0 && errorCount === 0 ? (
          <>
            <Divider layout="vertical" style={{ height: 20, margin: "0 2px" }} />
            <Tooltip content="跳转到第一个警告节点" position="bottom">
              <Tag
                size="small"
                style={{ ...warningTagStyle }}
                onClick={() => {
                  if (firstWarningObjectId) props.onNavigateToIssue?.(firstWarningObjectId);
                  props.onOpenProblemsPanel?.();
                }}
              >
                ⚠ {warningCount} 警告
              </Tag>
            </Tooltip>
          </>
        ) : null}
      </Space>
      {/* Run controls on the right */}
      {(props.onRun || props.isRunning) ? (
        <Space spacing={4}>
          {props.isRunning ? (
            <>
              <Spin size="small" />
              <Tag size="small" color="blue">执行中…</Tag>
            </>
          ) : null}
          {props.isRunning && props.onStopRun ? (
            <Tooltip content="停止执行" position="bottom">
              <Button
                icon={<IconStop />}
                size="small"
                type="danger"
                theme="light"
                aria-label="停止执行"
                onClick={props.onStopRun}
              />
            </Tooltip>
          ) : null}
          {!props.isRunning && props.onRun ? (
            <Tooltip content="测试运行 (Ctrl+Enter)" position="bottom">
              <Button
                icon={<IconPlay />}
                size="small"
                type="primary"
                theme="solid"
                aria-label="测试运行"
                disabled={props.readonly}
                onClick={props.onRun}
              >
                运行
              </Button>
            </Tooltip>
          ) : null}
        </Space>
      ) : null}
    </div>
  );
}
