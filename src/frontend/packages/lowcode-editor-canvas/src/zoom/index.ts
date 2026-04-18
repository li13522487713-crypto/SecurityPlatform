/**
 * 缩放控制（M04 C04-6）。
 *
 * - 范围 25% - 400%
 * - 适应屏幕 / 实际大小 / 自定义
 *
 * 纯函数 + 状态可由调用方接入 zustand store。
 */

export const ZOOM_MIN = 0.25;
export const ZOOM_MAX = 4;
export const ZOOM_STEPS = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 2, 3, 4] as const;

export function clampZoom(v: number): number {
  if (Number.isNaN(v)) return 1;
  return Math.min(Math.max(v, ZOOM_MIN), ZOOM_MAX);
}

export function zoomIn(current: number): number {
  const sorted = [...ZOOM_STEPS];
  for (const step of sorted) {
    if (step > current) return clampZoom(step);
  }
  return clampZoom(current);
}

export function zoomOut(current: number): number {
  const sorted = [...ZOOM_STEPS].reverse();
  for (const step of sorted) {
    if (step < current) return clampZoom(step);
  }
  return clampZoom(current);
}

export function fitToScreen(canvasWidth: number, canvasHeight: number, viewportWidth: number, viewportHeight: number): number {
  if (canvasWidth <= 0 || canvasHeight <= 0) return 1;
  const ratio = Math.min(viewportWidth / canvasWidth, viewportHeight / canvasHeight);
  return clampZoom(ratio);
}

export const RESET_ZOOM = 1;
