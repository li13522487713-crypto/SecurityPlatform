const FLOWGRAM_PAN_EXEMPT_SELECTOR =
  ".microflow-flowgram-node, .microflow-flowgram-canvas-controls, .microflow-flowgram-toolbar, .microflow-flowgram-status-strip, .microflow-flowgram-minimap, .semi-popover, .semi-dropdown, .semi-modal";

/** 空格键按住时排除节点本身，让空格拖动全局生效 */
const FLOWGRAM_PAN_EXEMPT_SELECTOR_SPACE =
  ".microflow-flowgram-canvas-controls, .microflow-flowgram-toolbar, .microflow-flowgram-status-strip, .microflow-flowgram-minimap, .semi-popover, .semi-dropdown, .semi-modal";

const FLOWGRAM_NODE_HIT_SELECTOR =
  "[data-microflow-object-id], .microflow-flowgram-node, .workflow-node-render, .workflow-port-render";

function isFlowgramPanExemptTarget(target: HTMLElement | undefined, spaceActive = false): boolean {
  const selector = spaceActive ? FLOWGRAM_PAN_EXEMPT_SELECTOR_SPACE : FLOWGRAM_PAN_EXEMPT_SELECTOR;
  return Boolean(target?.closest(selector));
}

function isFlowgramNodeTarget(target: HTMLElement | undefined): boolean {
  return Boolean(target?.closest(FLOWGRAM_NODE_HIT_SELECTOR));
}

export function shouldViewportPanFromPointerDown(args: {
  target: HTMLElement | undefined;
  button: number;
  panToolActive: boolean;
  spacePressed: boolean;
  draggingNode: boolean;
}): boolean {
  const { target, button, panToolActive, spacePressed, draggingNode } = args;
  if (draggingNode) {
    return false;
  }
  const nodeHit = isFlowgramNodeTarget(target);
  // 小手模式下优先拖拽节点；空格临时平移保持全局优先。
  if (nodeHit && panToolActive && !spacePressed) {
    return false;
  }
  const exempt = isFlowgramPanExemptTarget(target, spacePressed);
  return !exempt && (button === 1 || (button === 0 && (panToolActive || spacePressed)));
}

export function zoomViewportAtLocalPoint(
  viewport: { x: number; y: number; zoom: number },
  localX: number,
  localY: number,
  nextZoom: number,
): { x: number; y: number; zoom: number } {
  if (Math.abs(nextZoom - viewport.zoom) < 1e-6) {
    return { ...viewport, zoom: nextZoom };
  }
  const ratio = nextZoom / viewport.zoom;
  return {
    x: ratio * (localX + viewport.x) - localX,
    y: ratio * (localY + viewport.y) - localY,
    zoom: nextZoom,
  };
}

export function zoomViewportAtCanvasCenter(
  viewport: { x: number; y: number; zoom: number },
  containerWidth: number,
  containerHeight: number,
  nextZoom: number,
): { x: number; y: number; zoom: number } {
  if (containerWidth <= 0 || containerHeight <= 0) {
    return { ...viewport, zoom: nextZoom };
  }
  return zoomViewportAtLocalPoint(viewport, containerWidth / 2, containerHeight / 2, nextZoom);
}

export function zoomViewportForPanToolWheel(
  viewport: { x: number; y: number; zoom: number },
  localX: number,
  localY: number,
  deltaY: number,
): { x: number; y: number; zoom: number } {
  const scale = Math.exp(-deltaY * 0.002);
  return zoomViewportAtLocalPoint(viewport, localX, localY, viewport.zoom * scale);
}

export function isPointerTargetPanExempt(target: HTMLElement | undefined, spacePressed: boolean): boolean {
  return isFlowgramPanExemptTarget(target, spacePressed);
}
