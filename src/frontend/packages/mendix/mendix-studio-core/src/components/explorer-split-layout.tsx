import { useCallback, useEffect, useState, type MouseEvent as ReactMouseEvent, type ReactNode } from "react";

const STORAGE_KEY = "lowcode-studio:mendix-layout:v1";
const DEFAULT_WIDTH_PX = 280;
const COLLAPSED_WIDTH_PX = 48;
const MIN_WIDTH_PX = 280;
const MAX_WIDTH_PX = 420;

interface StoredMendixLayout {
  leftCollapsed?: boolean;
  leftWidth?: number;
  explorerMode?: "expanded" | "collapsed" | "iconOnly";
  activeLeftTool?: "explorer" | "toolbox" | "none";
}

function clampWidth(n: number): number {
  return Math.min(MAX_WIDTH_PX, Math.max(MIN_WIDTH_PX, Math.round(n)));
}

type ExplorerSplitMode = "normal" | "microflowDesigner";

function readStoredLayout(defaultCollapsed = false): Required<Pick<StoredMendixLayout, "leftCollapsed" | "leftWidth">> & StoredMendixLayout {
  if (typeof window === "undefined") {
    return { leftCollapsed: defaultCollapsed, leftWidth: DEFAULT_WIDTH_PX };
  }
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw) as StoredMendixLayout;
      const storedCollapsed = typeof parsed.leftCollapsed === "boolean"
        ? parsed.leftCollapsed
        : parsed.explorerMode === "collapsed" || parsed.explorerMode === "iconOnly";
      return {
        ...parsed,
        leftCollapsed: storedCollapsed,
        leftWidth: Number.isFinite(parsed.leftWidth) ? clampWidth(Number(parsed.leftWidth)) : DEFAULT_WIDTH_PX,
      };
    }
  } catch {
    /* ignore */
  }
  return { leftCollapsed: defaultCollapsed, leftWidth: DEFAULT_WIDTH_PX };
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
  mode?: ExplorerSplitMode;
  defaultCollapsed?: boolean;
  activeTool?: "explorer" | "toolbox" | "none";
}

/**
 * 左侧 App Explorer 与主工作区之间的可拖动分隔条，宽度持久化到 localStorage。
 */
export function ExplorerSplitLayout({ explorer, children, mode = "normal", defaultCollapsed, activeTool = "explorer" }: ExplorerSplitLayoutProps) {
  const initialCollapsed = defaultCollapsed ?? false;
  const [layout, setLayout] = useState(() => readStoredLayout(initialCollapsed));
  const [isTempExpanded, setIsTempExpanded] = useState(false);
  const isEffectivelyCollapsed = layout.leftCollapsed && !isTempExpanded;
  const widthPx = isEffectivelyCollapsed ? COLLAPSED_WIDTH_PX : layout.leftWidth;

  useEffect(() => {
    const resetTempExpanded = () => setIsTempExpanded(false);
    window.addEventListener("blur", resetTempExpanded);
    return () => window.removeEventListener("blur", resetTempExpanded);
  }, []);

  useEffect(() => {
    const timer = window.setTimeout(() => writeStoredLayout({
      ...layout,
      explorerMode: isEffectivelyCollapsed ? "iconOnly" : "expanded",
      activeLeftTool: activeTool,
    }), 160);
    return () => window.clearTimeout(timer);
  }, [activeTool, isEffectivelyCollapsed, layout]);

  const onGutterMouseDown = useCallback(
    (event: ReactMouseEvent<HTMLDivElement>) => {
      event.preventDefault();
      const startX = event.clientX;
      const startW = widthPx;
      const onMove = (ev: globalThis.MouseEvent) => {
        const delta = ev.clientX - startX;
        setLayout(current => ({ ...current, leftCollapsed: false, leftWidth: clampWidth(startW + delta) }));
        setIsTempExpanded(false);
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

  const handleToggleCollapse = () => {
    if (layout.leftCollapsed) {
      setIsTempExpanded(true);
      setLayout(current => ({ ...current, leftCollapsed: false }));
    } else {
      setIsTempExpanded(false);
      setLayout(current => ({ ...current, leftCollapsed: true }));
    }
  };

  return (
    <>
      <div
        className={`studio-explorer-split__pane${isEffectivelyCollapsed ? " studio-explorer-split__pane--collapsed" : ""}`}
        style={{ width: widthPx }}
        data-testid="mendix-studio-app-explorer"
        data-collapsed={isEffectivelyCollapsed ? "true" : "false"}
        data-mode={mode}
        data-active-tool={activeTool}
      >
        <button
          type="button"
          className="studio-explorer-split__collapse"
          aria-label={isEffectivelyCollapsed ? "展开 App Explorer" : "折叠 App Explorer"}
          title={isEffectivelyCollapsed ? "App Explorer" : "折叠 App Explorer"}
          onClick={handleToggleCollapse}
        >
          <span aria-hidden="true">☰</span>
        </button>
        <div className="studio-explorer-split__content">{explorer}</div>
      </div>
      {!isEffectivelyCollapsed ? (
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
