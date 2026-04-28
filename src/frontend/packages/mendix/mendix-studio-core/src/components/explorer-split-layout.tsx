import { useCallback, useEffect, useState, type MouseEvent as ReactMouseEvent, type ReactNode } from "react";

const STORAGE_KEY = "mendix_studio_explorer_width_px";
const DEFAULT_WIDTH_PX = 260;
const MIN_WIDTH_PX = 200;
const MAX_WIDTH_PX = 560;

function clampWidth(n: number): number {
  return Math.min(MAX_WIDTH_PX, Math.max(MIN_WIDTH_PX, Math.round(n)));
}

function readStoredWidth(): number {
  if (typeof window === "undefined") {
    return DEFAULT_WIDTH_PX;
  }
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    const parsed = raw ? Number.parseInt(raw, 10) : NaN;
    if (Number.isFinite(parsed)) {
      return clampWidth(parsed);
    }
  } catch {
    /* ignore */
  }
  return DEFAULT_WIDTH_PX;
}

export interface ExplorerSplitLayoutProps {
  explorer: ReactNode;
  children: ReactNode;
}

/**
 * 左侧 App Explorer 与主工作区之间的可拖动分隔条，宽度持久化到 localStorage。
 */
export function ExplorerSplitLayout({ explorer, children }: ExplorerSplitLayoutProps) {
  const [widthPx, setWidthPx] = useState(readStoredWidth);

  useEffect(() => {
    try {
      window.localStorage.setItem(STORAGE_KEY, String(widthPx));
    } catch {
      /* ignore */
    }
  }, [widthPx]);

  const onGutterMouseDown = useCallback(
    (event: ReactMouseEvent<HTMLDivElement>) => {
      event.preventDefault();
      const startX = event.clientX;
      const startW = widthPx;
      const onMove = (ev: globalThis.MouseEvent) => {
        const delta = ev.clientX - startX;
        setWidthPx(clampWidth(startW + delta));
      };
      const onUp = () => {
        document.removeEventListener("mousemove", onMove);
        document.removeEventListener("mouseup", onUp);
        document.body.style.removeProperty("cursor");
        document.body.style.removeProperty("user-select");
      };
      document.body.style.cursor = "col-resize";
      document.body.style.userSelect = "none";
      document.addEventListener("mousemove", onMove);
      document.addEventListener("mouseup", onUp);
    },
    [widthPx]
  );

  return (
    <>
      <div className="studio-explorer-split__pane" style={{ width: widthPx }}>
        {explorer}
      </div>
      <div
        className="studio-explorer-split__gutter"
        role="separator"
        aria-orientation="vertical"
        aria-label="调整资源管理器宽度"
        onMouseDown={onGutterMouseDown}
      />
      {children}
    </>
  );
}
