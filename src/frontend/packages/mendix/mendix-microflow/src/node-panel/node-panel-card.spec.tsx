// @vitest-environment jsdom

import { cleanup, fireEvent, render } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", async () => {
  const React = await import("react");
  const Button = ({
    children,
    icon,
    ...props
  }: React.ButtonHTMLAttributes<HTMLButtonElement> & { icon?: React.ReactNode }) => (
    <button {...props}>
      {icon}
      {children}
    </button>
  );
  return {
    Button,
    Empty: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
    Input: (props: React.InputHTMLAttributes<HTMLInputElement> & { onChange?: (value: string) => void }) => (
      <input {...props} onChange={event => props.onChange?.(event.currentTarget.value)} />
    ),
    Popover: ({ children }: { children?: React.ReactNode }) => <span data-testid="mock-popover">{children}</span>,
    Space: ({ children }: { children?: React.ReactNode }) => <span>{children}</span>,
    Tabs: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
    Tag: ({ children }: { children?: React.ReactNode }) => <span>{children}</span>,
    Toast: { success: vi.fn(), warning: vi.fn() },
    Typography: {
      Text: ({ children, ...props }: React.HTMLAttributes<HTMLSpanElement>) => <span {...props}>{children}</span>,
    },
  };
});

vi.mock("@douyinfe/semi-icons", async () => {
  const React = await import("react");
  const Icon = () => <span />;
  return {
    IconChevronDown: Icon,
    IconChevronRight: Icon,
    IconCopy: Icon,
    IconFilter: Icon,
    IconInfoCircle: Icon,
    IconPlus: Icon,
    IconSearch: Icon,
    IconStar: Icon,
    IconStarStroked: Icon,
    IconStop: Icon,
  };
});

import {
  defaultMicroflowNodePanelRegistry,
  getMicroflowNodeRegistryKey,
  hasMicroflowNodeDragType,
  MICROFLOW_NODE_DND_TYPE,
  readMicroflowNodeDragPayload,
} from "../node-registry";
import { MicroflowNodeCard, defaultMicroflowNodePanelLabels } from "./index";

function registry(key: string) {
  const item = defaultMicroflowNodePanelRegistry.find(entry => getMicroflowNodeRegistryKey(entry) === key);
  if (!item) {
    throw new Error(`Missing registry item ${key}`);
  }
  return item;
}

function dragData() {
  const data = new Map<string, string>();
  const dataTransfer = {
    types: [] as string[],
    effectAllowed: "none",
    setData: (type: string, value: string) => {
      data.set(type, value);
      if (!dataTransfer.types.includes(type)) {
        dataTransfer.types.push(type);
      }
    },
    getData: (type: string) => data.get(type) ?? "",
  };
  return {
    data,
    dataTransfer,
  };
}

afterEach(() => cleanup());

describe("MicroflowNodeCard", () => {
  const createContext = { microflowId: "mf-test", schemaLoaded: true };

  it("renders without a hover tooltip wrapper", () => {
    const { container } = render(
      <MicroflowNodeCard
        item={registry("activity:logMessage")}
        favorite={false}
        labels={defaultMicroflowNodePanelLabels}
        onAdd={vi.fn()}
        onFavoriteToggle={vi.fn()}
        onContextMenu={vi.fn()}
        createContext={createContext}
      />,
    );
    expect(container.querySelector(".semi-tooltip")).toBeNull();
    expect(container.textContent).toContain("Log Message");
  });

  it("adds nodes on double click instead of single click", () => {
    const onAdd = vi.fn();
    const { getByTestId } = render(
      <MicroflowNodeCard
        item={registry("activity:logMessage")}
        favorite={false}
        labels={defaultMicroflowNodePanelLabels}
        onAdd={onAdd}
        onFavoriteToggle={vi.fn()}
        onContextMenu={vi.fn()}
        createContext={createContext}
      />,
    );
    const card = getByTestId("microflow-node-panel-item-activity-logMessage");
    fireEvent.click(card);
    expect(onAdd).not.toHaveBeenCalled();
    fireEvent.doubleClick(card);
    expect(onAdd).toHaveBeenCalledTimes(1);
  });

  it("writes complete drag payloads for draggable nodes and suppresses click add while dragging", () => {
    const onAdd = vi.fn();
    const onStartDrag = vi.fn();
    const { data, dataTransfer } = dragData();
    const { getByTestId } = render(
      <MicroflowNodeCard
        item={registry("activity:logMessage")}
        favorite={false}
        labels={defaultMicroflowNodePanelLabels}
        onAdd={onAdd}
        onFavoriteToggle={vi.fn()}
        onContextMenu={vi.fn()}
        onStartDrag={onStartDrag}
        createContext={createContext}
      />,
    );
    const card = getByTestId("microflow-node-panel-item-activity-logMessage");
    fireEvent.dragStart(card, { dataTransfer });
    fireEvent.click(card);

    const customPayload = JSON.parse(data.get(MICROFLOW_NODE_DND_TYPE) ?? "{}") as { registryKey?: string; dragType?: string };
    const jsonPayload = JSON.parse(data.get("application/json") ?? "{}") as { registryKey?: string; dragType?: string };
    expect(customPayload).toMatchObject({ dragType: "microflow-node", registryKey: "activity:logMessage" });
    expect(jsonPayload).toMatchObject({ dragType: "microflow-node", registryKey: "activity:logMessage" });
    expect(data.get("text/plain")).toBe("activity:logMessage");
    expect(onStartDrag).toHaveBeenCalledWith(expect.objectContaining({ registryKey: "activity:logMessage" }));
    expect(onAdd).not.toHaveBeenCalled();
  });

  it("does not write drag data for disabled nodes", () => {
    const { data, dataTransfer } = dragData();
    const { getByTestId } = render(
      <MicroflowNodeCard
        item={registry("activity:callNanoflow")}
        favorite={false}
        labels={defaultMicroflowNodePanelLabels}
        onAdd={vi.fn()}
        onFavoriteToggle={vi.fn()}
        onContextMenu={vi.fn()}
        createContext={createContext}
      />,
    );
    fireEvent.dragStart(getByTestId("microflow-node-panel-item-activity-callNanoflow"), { dataTransfer });
    expect(data.size).toBe(0);
  });
});

describe("microflow node drag payload helpers", () => {
  it("detects drag types without reading drag data during dragover", () => {
    const getData = vi.fn();
    const dataTransfer = { types: [MICROFLOW_NODE_DND_TYPE], getData } as unknown as DataTransfer;
    expect(hasMicroflowNodeDragType(dataTransfer)).toBe(true);
    expect(getData).not.toHaveBeenCalled();
  });

  it("reads custom, json, and text/plain payloads during drop", () => {
    const payload = {
      dragType: "microflow-node",
      registryKey: "activity:logMessage",
      source: "node-panel",
    };
    const custom = {
      getData: (type: string) => type === MICROFLOW_NODE_DND_TYPE ? JSON.stringify(payload) : "",
    } as DataTransfer;
    expect(readMicroflowNodeDragPayload(custom)?.registryKey).toBe("activity:logMessage");

    const json = {
      getData: (type: string) => type === "application/json" ? JSON.stringify(payload) : "",
    } as DataTransfer;
    expect(readMicroflowNodeDragPayload(json)?.registryKey).toBe("activity:logMessage");

    const text = {
      getData: (type: string) => type === "text/plain" ? "activity:logMessage" : "",
    } as DataTransfer;
    expect(readMicroflowNodeDragPayload(text)?.registryKey).toBe("activity:logMessage");
  });
});
