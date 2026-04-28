import { useEffect, useMemo, useRef, useState, type CSSProperties, type ReactNode, type RefObject } from "react";
import "./responsive-layout.css";

export type ResponsiveBreakpoint = "narrow" | "compact" | "desktop" | "wide";

export interface ElementSize {
  width: number;
  height: number;
}

export interface ResponsiveBreakpointOptions {
  compact?: number;
  desktop?: number;
  wide?: number;
}

const DEFAULT_BREAKPOINTS: Required<ResponsiveBreakpointOptions> = {
  compact: 1024,
  desktop: 1280,
  wide: 1600
};

export function useElementSize<T extends HTMLElement>(ref: RefObject<T>): ElementSize {
  const [size, setSize] = useState<ElementSize>({ width: 0, height: 0 });

  useEffect(() => {
    const element = ref.current;
    if (!element || typeof ResizeObserver === "undefined") {
      return undefined;
    }

    const observer = new ResizeObserver(entries => {
      const rect = entries[0]?.contentRect;
      if (!rect) return;
      setSize(current => {
        const width = Math.round(rect.width);
        const height = Math.round(rect.height);
        return current.width === width && current.height === height ? current : { width, height };
      });
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, [ref]);

  return size;
}

export function getResponsiveBreakpoint(width: number, options: ResponsiveBreakpointOptions = {}): ResponsiveBreakpoint {
  const breakpoints = { ...DEFAULT_BREAKPOINTS, ...options };
  if (width >= breakpoints.wide) return "wide";
  if (width >= breakpoints.desktop) return "desktop";
  if (width >= breakpoints.compact) return "compact";
  return "narrow";
}

export function useResponsiveBreakpoint<T extends HTMLElement>(
  options?: ResponsiveBreakpointOptions
): { ref: RefObject<T>; size: ElementSize; breakpoint: ResponsiveBreakpoint } {
  const ref = useRef<T>(null);
  const size = useElementSize(ref);
  const breakpoint = useMemo(() => getResponsiveBreakpoint(size.width, options), [options, size.width]);

  return { ref, size, breakpoint };
}

export interface ResponsivePageFrameProps {
  containerRef?: RefObject<HTMLDivElement>;
  className?: string;
  style?: CSSProperties;
  header?: ReactNode;
  children?: ReactNode;
  breakpoint?: ResponsiveBreakpoint;
}

export function ResponsivePageFrame({ containerRef, className, style, header, children, breakpoint }: ResponsivePageFrameProps) {
  return (
    <div
      ref={containerRef}
      className={["atlas-responsive-page-frame", className].filter(Boolean).join(" ")}
      data-responsive-breakpoint={breakpoint}
      style={style}
    >
      {header ? <div className="atlas-responsive-page-frame__header">{header}</div> : null}
      <div className="atlas-responsive-page-frame__body">{children}</div>
    </div>
  );
}

export function ResponsiveToolbar({
  className,
  main,
  actions
}: {
  className?: string;
  main?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <div className={["atlas-responsive-toolbar", className].filter(Boolean).join(" ")}>
      <div className="atlas-responsive-toolbar__main">{main}</div>
      <div className="atlas-responsive-toolbar__actions">{actions}</div>
    </div>
  );
}

export function ResponsiveSummaryCards({
  className,
  minCardWidth = 220,
  children
}: {
  className?: string;
  minCardWidth?: number;
  children?: ReactNode;
}) {
  return (
    <div
      className={["atlas-responsive-summary-cards", className].filter(Boolean).join(" ")}
      style={{ "--atlas-summary-card-min": `${minCardWidth}px` } as CSSProperties}
    >
      {children}
    </div>
  );
}

export function ResponsiveBottomDock({ className, children }: { className?: string; children?: ReactNode }) {
  return (
    <div className={["atlas-responsive-bottom-dock", className].filter(Boolean).join(" ")}>
      {children}
    </div>
  );
}
