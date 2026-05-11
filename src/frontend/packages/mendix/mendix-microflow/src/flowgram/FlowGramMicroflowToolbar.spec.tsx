// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { FlowGramMicroflowToolbar } from "./FlowGramMicroflowToolbar";

vi.mock("@flowgram-adapter/free-layout-editor", () => ({
  WorkflowResetLayoutService: class {},
  usePlayground: () => ({
    config: {
      zoomin: vi.fn(),
      zoomout: vi.fn(),
      updateConfig: vi.fn(),
      zoom: vi.fn(),
    },
  }),
  useService: () => ({
    fitView: vi.fn(),
  }),
}));

vi.mock("../i18n/copy", () => ({
  getMendixMicroflowCopy: () => ({
    canvasToolbar: {
      panToolTooltip: "pan",
      panTool: "Pan Tool",
      zoomOutTooltip: "zoom out",
      zoomOut: "zoom out",
      zoomResetTooltip: "zoom reset",
      zoomReset: "zoom reset",
      zoomInTooltip: "zoom in",
      zoomIn: "zoom in",
      fitViewTooltip: "fit",
      fitView: "fit",
      centerViewTooltip: "center",
      centerView: "center",
      undoTooltip: "undo",
      undo: "undo",
      redoTooltip: "redo",
      redo: "redo",
      gridTooltip: "grid",
      grid: "grid",
      minimapTooltip: "minimap",
      minimap: "minimap",
      autoLayoutTooltip: "auto",
      autoLayout: "auto",
      zoomLevels: [
        { label: "50%", value: 0.5 },
        { label: "100%", value: 1 },
      ],
    },
  }),
}));

vi.mock("@douyinfe/semi-icons", () => ({
  IconClock: () => <span>clock</span>,
  IconGridRectangle: () => <span>grid</span>,
  IconHandle: () => <span>handle</span>,
  IconMapPin: () => <span>pin</span>,
  IconMinus: () => <span>minus</span>,
  IconPlus: () => <span>plus</span>,
  IconRedo: () => <span>redo</span>,
  IconRefresh: () => <span>refresh</span>,
  IconTreeTriangleDown: () => <span>tree</span>,
  IconUndo: () => <span>undo</span>,
}));

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, onClick, ...rest }: any) => <button type="button" onClick={onClick} {...rest}>{children}</button>,
  Divider: () => <span>|</span>,
  Dropdown: ({ children }: any) => <span>{children}</span>,
  Space: ({ children }: any) => <div>{children}</div>,
  Tag: ({ children, onClick }: any) => (
    <button type="button" onClick={onClick}>
      {children}
    </button>
  ),
  Tooltip: ({ children }: any) => <>{children}</>,
}));

afterEach(() => cleanup());

function baseProps() {
  return {
    readonly: false,
    canUndo: true,
    canRedo: true,
    onUndo: vi.fn(),
    onRedo: vi.fn(),
    onAutoLayout: vi.fn(),
    autoLayoutLoading: false,
    viewport: { x: 0, y: 0, zoom: 1 },
    onViewportChange: vi.fn(),
    onFitView: vi.fn(),
    onCenterView: vi.fn(),
    gridEnabled: true,
    onToggleGrid: vi.fn(),
    miniMapVisible: false,
    onToggleMiniMap: vi.fn(),
    dirty: false,
    saving: false,
    validating: false,
    validationIssues: [],
    onOpenProblemsPanel: vi.fn(),
  } as const;
}

describe("FlowGramMicroflowToolbar node-count badge", () => {
  it("renders green badge for complexity below warning threshold", () => {
    render(
      <FlowGramMicroflowToolbar
        {...baseProps()}
        microflowComplexity={{
          totalElements: 12,
          activityCount: 8,
          decisionCount: 1,
          hasAnnotation: true,
          annotationRecommended: false,
          level: "ok",
          recommendedMaxNodes: 25,
        }}
      />,
    );

    expect(screen.getByText("✓ 12")).toBeTruthy();
  });

  it("renders orange badge for warning threshold", () => {
    render(
      <FlowGramMicroflowToolbar
        {...baseProps()}
        microflowComplexity={{
          totalElements: 22,
          activityCount: 15,
          decisionCount: 2,
          hasAnnotation: true,
          annotationRecommended: true,
          level: "warning",
          recommendedMaxNodes: 25,
        }}
      />,
    );

    expect(screen.getByText("⚠ 22 / 25")).toBeTruthy();
  });

  it("renders red badge with split recommendation for error threshold", () => {
    render(
      <FlowGramMicroflowToolbar
        {...baseProps()}
        microflowComplexity={{
          totalElements: 27,
          activityCount: 20,
          decisionCount: 4,
          hasAnnotation: false,
          annotationRecommended: true,
          level: "error",
          recommendedMaxNodes: 25,
        }}
      />,
    );

    expect(screen.getByText("✕ 27 / 25")).toBeTruthy();
  });

  it("opens Problems panel when clicking the node-count badge", () => {
    const onOpenProblemsPanel = vi.fn();
    render(
      <FlowGramMicroflowToolbar
        {...baseProps()}
        onOpenProblemsPanel={onOpenProblemsPanel}
        microflowComplexity={{
          totalElements: 22,
          activityCount: 15,
          decisionCount: 2,
          hasAnnotation: true,
          annotationRecommended: true,
          level: "warning",
          recommendedMaxNodes: 25,
        }}
      />,
    );

    fireEvent.click(screen.getByText("⚠ 22 / 25"));
    expect(onOpenProblemsPanel).toHaveBeenCalledTimes(1);
  });
});
