/**
 * 对齐线 / 吸附线 / 参考线 / 网格 / 智能距离提示（M04 C04-3）。
 *
 * 纯几何计算函数，不依赖 DOM；供 M07 / M08 渲染层调用。
 */

export interface Rect {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface AlignmentGuide {
  /** 对齐方向：horizontal（水平线 y）或 vertical（垂直线 x）。*/
  axis: 'horizontal' | 'vertical';
  /** 对齐位置（坐标值）。*/
  position: number;
  /** 来源对象（其它组件 id）。*/
  sourceId: string;
}

const SNAP_TOLERANCE = 4;

/**
 * 给定移动中的矩形 movingRect 与其它静态矩形列表 staticRects，
 * 返回与 movingRect 当前位置最近且在 SNAP_TOLERANCE 内的对齐线 + 吸附后的新位置。
 */
export function computeSnapAndGuides(movingRect: Rect, staticRects: ReadonlyArray<Rect & { id: string }>): { snapped: Rect; guides: AlignmentGuide[] } {
  let nx = movingRect.x;
  let ny = movingRect.y;
  const guides: AlignmentGuide[] = [];

  // 计算 movingRect 的边/中心
  const movLeft = movingRect.x;
  const movRight = movingRect.x + movingRect.width;
  const movTop = movingRect.y;
  const movBottom = movingRect.y + movingRect.height;
  const movCx = movingRect.x + movingRect.width / 2;
  const movCy = movingRect.y + movingRect.height / 2;

  let bestDx: { delta: number; guide: AlignmentGuide } | null = null;
  let bestDy: { delta: number; guide: AlignmentGuide } | null = null;

  for (const s of staticRects) {
    const sLeft = s.x;
    const sRight = s.x + s.width;
    const sTop = s.y;
    const sBottom = s.y + s.height;
    const sCx = s.x + s.width / 2;
    const sCy = s.y + s.height / 2;

    // 垂直对齐（x 方向）
    const xCandidates: Array<{ delta: number; pos: number }> = [
      { delta: sLeft - movLeft, pos: sLeft },
      { delta: sRight - movRight, pos: sRight },
      { delta: sCx - movCx, pos: sCx },
      { delta: sLeft - movRight, pos: sLeft },
      { delta: sRight - movLeft, pos: sRight }
    ];
    for (const c of xCandidates) {
      if (Math.abs(c.delta) <= SNAP_TOLERANCE && (!bestDx || Math.abs(c.delta) < Math.abs(bestDx.delta))) {
        bestDx = { delta: c.delta, guide: { axis: 'vertical', position: c.pos, sourceId: s.id } };
      }
    }

    // 水平对齐（y 方向）
    const yCandidates: Array<{ delta: number; pos: number }> = [
      { delta: sTop - movTop, pos: sTop },
      { delta: sBottom - movBottom, pos: sBottom },
      { delta: sCy - movCy, pos: sCy },
      { delta: sTop - movBottom, pos: sTop },
      { delta: sBottom - movTop, pos: sBottom }
    ];
    for (const c of yCandidates) {
      if (Math.abs(c.delta) <= SNAP_TOLERANCE && (!bestDy || Math.abs(c.delta) < Math.abs(bestDy.delta))) {
        bestDy = { delta: c.delta, guide: { axis: 'horizontal', position: c.pos, sourceId: s.id } };
      }
    }
  }

  if (bestDx) {
    nx += bestDx.delta;
    guides.push(bestDx.guide);
  }
  if (bestDy) {
    ny += bestDy.delta;
    guides.push(bestDy.guide);
  }

  return { snapped: { ...movingRect, x: nx, y: ny }, guides };
}

/** 网格吸附：将位置吸附到最近的网格点。*/
export function snapToGrid(point: { x: number; y: number }, gridSize: number): { x: number; y: number } {
  if (gridSize <= 1) return point;
  return {
    x: Math.round(point.x / gridSize) * gridSize,
    y: Math.round(point.y / gridSize) * gridSize
  };
}
