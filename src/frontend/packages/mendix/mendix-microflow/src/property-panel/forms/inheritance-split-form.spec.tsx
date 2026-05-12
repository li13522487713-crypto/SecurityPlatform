// @vitest-environment jsdom

import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { fireEvent } from "@testing-library/react";
import { InheritanceSplitForm } from "./inheritance-split-form";

vi.mock("@douyinfe/semi-ui", () => ({
  Input: ({ value, onChange, disabled }: any) => <input value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
  Select: ({ value, onChange, optionList, disabled, multiple }: any) => (
    <select
      multiple={Boolean(multiple)}
      value={value ?? (multiple ? [] : "")}
      disabled={disabled}
      onChange={event => onChange?.(multiple ? Array.from(event.currentTarget.selectedOptions).map(option => option.value) : event.currentTarget.value)}
    >
      {(optionList ?? []).map((option: any) => <option key={option.value} value={option.value}>{option.label}</option>)}
    </select>
  ),
  TextArea: ({ value, disabled }: any) => <textarea value={value ?? ""} disabled={disabled} readOnly />,
  Tooltip: ({ children }: any) => <>{children}</>,
  Typography: {
    Text: ({ children }: any) => <span>{children}</span>,
  },
}));

vi.mock("../selectors", () => ({
  EntitySelector: ({ value, onChange, disabled }: any) => (
    <select value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)}>
      <option value="">(none)</option>
      <option value="University.Member">University.Member</option>
    </select>
  ),
}));

afterEach(() => cleanup());

describe("InheritanceSplitForm", () => {
  it("shows branch coverage warnings for missing specialization and missing empty branch", () => {
    render(
      <InheritanceSplitForm
        props={baseProps(withFlows([
          flow("f-student", [{ kind: "inheritance", officialType: "Microflows$InheritanceCase", entityQualifiedName: "University.Student" }]),
        ]))}
        object={inheritanceObject() as any}
        issues={[] as any}
        metadata={metadata() as any}
        patch={vi.fn()}
        onAddFlow={vi.fn()}
      />,
    );

    expect(screen.getByText("Missing specialization branches: University.Teacher")).toBeTruthy();
    expect(screen.getByText("Missing (empty) branch for unmatched or empty object type.")).toBeTruthy();
  });

  it("does not show missing warnings when specialization and empty branches are covered", () => {
    render(
      <InheritanceSplitForm
        props={baseProps(withFlows([
          flow("f-student", [{ kind: "inheritance", officialType: "Microflows$InheritanceCase", entityQualifiedName: "University.Student" }]),
          flow("f-teacher", [{ kind: "inheritance", officialType: "Microflows$InheritanceCase", entityQualifiedName: "University.Teacher" }]),
          flow("f-empty", [{ kind: "empty", officialType: "Microflows$NoCase" }]),
        ]))}
        object={inheritanceObject() as any}
        issues={[] as any}
        metadata={metadata() as any}
        patch={vi.fn()}
        onAddFlow={vi.fn()}
      />,
    );

    expect(screen.queryByText("Missing specialization branches: University.Teacher")).toBeNull();
    expect(screen.queryByText("Missing (empty) branch for unmatched or empty object type.")).toBeNull();
  });

  it("treats legacy fallback branches as the empty branch in coverage summary", () => {
    render(
      <InheritanceSplitForm
        props={baseProps(withFlows([
          flow("f-student", [{ kind: "inheritance", officialType: "Microflows$InheritanceCase", entityQualifiedName: "University.Student" }]),
          flow("f-teacher", [{ kind: "inheritance", officialType: "Microflows$InheritanceCase", entityQualifiedName: "University.Teacher" }]),
          flow("f-fallback", [{ kind: "fallback", officialType: "Microflows$NoCase" }]),
        ]))}
        object={inheritanceObject() as any}
        issues={[] as any}
        metadata={metadata() as any}
        patch={vi.fn()}
        onAddFlow={vi.fn()}
      />,
    );

    expect(screen.queryByText("Missing (empty) branch for unmatched or empty object type.")).toBeNull();
    const coverage = screen.getAllByRole("textbox").find(element => element.tagName === "TEXTAREA") as HTMLTextAreaElement | undefined;
    expect(coverage?.value).toContain("f-fallback: (empty)");
  });

  it("adds missing specialization and empty branches", () => {
    const onAddFlow = vi.fn();
    render(
      <InheritanceSplitForm
        props={baseProps(withFlows([
          flow("f-student", [{ kind: "inheritance", officialType: "Microflows$InheritanceCase", entityQualifiedName: "University.Student" }]),
          flow("f-empty", [{ kind: "empty", officialType: "Microflows$NoCase" }]),
        ], true))}
        object={inheritanceObject() as any}
        issues={[] as any}
        metadata={metadata() as any}
        patch={vi.fn()}
        onAddFlow={onAddFlow}
      />,
    );

    const button = screen.getByText("Add missing branches");
    fireEvent.click(button);

    expect(onAddFlow).toHaveBeenCalledTimes(1);
    expect(onAddFlow).toHaveBeenCalledWith(expect.objectContaining({
      caseValues: [expect.objectContaining({ kind: "inheritance", entityQualifiedName: "University.Teacher" })],
    }));
  });
});

function flow(id: string, caseValues: unknown[]) {
  return {
    id,
    kind: "sequence",
    originObjectId: "inheritance-1",
    destinationObjectId: "target",
    caseValues,
    editor: { edgeKind: "objectTypeCondition" },
  };
}

function withFlows(flows: unknown[], includeTargetNode = false) {
  const objects: unknown[] = includeTargetNode
    ? [
      inheritanceObject(),
      {
        id: "target-end",
        kind: "endEvent",
        officialType: "Microflows$EndEvent",
        officialName: "End",
        relativeMiddlePoint: { x: 200, y: 0 },
        size: { width: 60, height: 60 },
        editor: {},
      },
    ]
    : [];
  return {
    schemaVersion: "1.0.0",
    id: "mf",
    name: "MF",
    objectCollection: {
      id: "root",
      officialType: "Microflows$MicroflowObjectCollection",
      objects,
      flows,
    },
    flows,
    parameters: [],
  };
}

function baseProps(schema: unknown) {
  return {
    readonly: false,
    schema,
  };
}

function inheritanceObject() {
  return {
    id: "inheritance-1",
    kind: "inheritanceSplit",
    officialType: "Microflows$InheritanceSplit",
    inputObjectVariableName: "$member",
    generalizedEntityQualifiedName: "University.Member",
    allowedSpecializations: ["University.Student", "University.Teacher"],
    entity: {
      generalizedEntityQualifiedName: "University.Member",
      allowedSpecializations: ["University.Student", "University.Teacher"],
    },
    errorHandlingType: "rollback",
    editor: {},
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 120, height: 80 },
    ports: [],
  };
}

function metadata() {
  return {
    entities: [
      {
        qualifiedName: "University.Member",
        name: "Member",
        specializations: ["University.Student", "University.Teacher"],
      },
    ],
    enumerations: [],
    microflows: [],
  };
}
