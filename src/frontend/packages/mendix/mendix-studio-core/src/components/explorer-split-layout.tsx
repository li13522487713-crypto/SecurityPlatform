import { useCallback, useEffect, useState, type MouseEvent as ReactMouseEvent, type ReactNode } from "react";

const STORAGE_KEY = "lowcode-studio:mendix-layout:v1";
const DEFAULT_WIDTH_PX = 280;
const COLLAPSED_WIDTH_PX = 48;
const MIN_WIDTH_PX = 280;
const MAX_WIDTH_PX = 420;

interface StoredMendixLayout {
  leftCollapsed?: boolean;
  leftWidth?: number;
}

function clampWidth(n: number): number {
  return Math.min(MAX_WIDTH_PX, Math.max(MIN_WIDTH_PX, Math.round(n)));
}

function readStoredLayout(): Required<StoredMendixLayout> {
  if (typeof window === "undefined") {
    return { leftCollapsed: false, leftWidth: DEFAULT_WIDTH_PX };
  }
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw) as StoredMendixLayout;
      return {
        leftCollapsed: Boolean(parsed.leftCollapsed),
        leftWidth: Number.isFinite(parsed.leftWidth) ? clampWidth(Number(parsed.leftWidth)) : DEFAULT_WIDTH_PX
      };
    }
  } catch {
    /* ignore */
  }
  return { leftCollapsed: false, leftWidth: DEFAULT_WIDTH_PX };
}

function writeStoredLayout(patch: StoredMendixLayout): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    const current = JSON.parse(window.localStorage.getItem(STORAGE_KEY) || "{}") as StoredMendixLayout;
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify({ ...current, ...patch }));
  } catch {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(patch));
  }
}

export interface ExplorerSplitLayoutProps {
  explorer: ReactNode;
  children: ReactNode;
}

/**
 * 左侧 App Explorer 与主工作区之间的可拖动分隔条，宽度持久化到 localStorage。
 */
export function ExplorerSplitLayout({ explorer, children }: ExplorerSplitLayoutProps) {
  const [layout, setLayout] = useState(readStoredLayout);
  const widthPx = layout.leftCollapsed ? COLLAPSED_WIDTH_PX : layout.leftWidth;

  useEffect(() => {
    const timer = window.setTimeout(() => writeStoredLayout(layout), 160);
    return () => window.clearTimeout(timer);
  }, [layout]);

  const onGutterMouseDown = useCallback(
    (event: ReactMouseEvent<HTMLDivElement>) => {
      event.preventDefault();
      const startX = event.clientX;
      const startW = widthPx;
      const onMove = (ev: globalThis.MouseEvent) => {
        const delta = ev.clientX - startX;
        setLayout(current => ({ ...current, leftCollapsed: false, leftWidth: clampWidth(startW + delta) }));
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
      <div
        className={`studio-explorer-split__pane${layout.leftCollapsed ? " studio-explorer-split__pane--collapsed" : ""}`}
        style={{ width: widthPx }}
        data-testid="mendix-studio-app-explorer"
        data-collapsed={layout.leftCollapsed ? "true" : "false"}
      >
        <button
          type="button"
          className="studio-explorer-split__collapse"
          aria-label={layout.leftCollapsed ? "展开 App Explorer" : "折叠 App Explorer"}
          title={layout.leftCollapsed ? "App Explorer" : "折叠 App Explorer"}
          onClick={() => setLayout(current => ({ ...current, leftCollapsed: !current.leftCollapsed }))}
        >
          <span aria-hidden="true">☰</span>
        </button>
        <div className="studio-explorer-split__content">{explorer}</div>
      </div>
      {!layout.leftCollapsed ? (
        <div
          className="studio-explorer-split__gutter"
          role="separator"
          aria-orientation="vertical"
          aria-label="调整资源管理器宽度"
          onMouseDown={onGutterMouseDown}
        />
      ) : null}
      {children}
    </>
  );
}
