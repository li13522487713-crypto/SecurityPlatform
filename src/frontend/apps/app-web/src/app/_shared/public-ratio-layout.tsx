import type { CSSProperties, ReactNode } from "react";
import "./public-ratio-layout.css";

type LayoutMode = "shell" | "full";

interface PublicRatioLayoutProps {
  mode?: LayoutMode;
  className?: string;
  style?: CSSProperties;
  testId?: string;
  children: ReactNode;
}

interface PublicRatioFrameProps {
  className?: string;
  style?: CSSProperties;
  children: ReactNode;
}

interface PublicRatioSplitProps {
  className?: string;
  style?: CSSProperties;
  children: ReactNode;
}

function joinClasses(...values: Array<string | undefined>): string {
  return values.filter(Boolean).join(" ");
}

export function PublicRatioLayout({
  mode = "shell",
  className,
  style,
  testId,
  children
}: PublicRatioLayoutProps) {
  return (
    <div
      className={joinClasses("atlas-layout", `atlas-layout--${mode}`, className)}
      style={style}
      data-testid={testId}
    >
      {children}
    </div>
  );
}

export function PublicRatioFrame({ className, style, children }: PublicRatioFrameProps) {
  return (
    <div className={joinClasses("atlas-layout__frame", className)} style={style}>
      {children}
    </div>
  );
}

export function PublicRatioSplit({ className, style, children }: PublicRatioSplitProps) {
  return (
    <div className={joinClasses("atlas-layout__split", className)} style={style}>
      {children}
    </div>
  );
}
