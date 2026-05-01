import { describe, expect, it } from "vitest";

import type { MicroflowAction } from "../schema/types";
import { tryMapP0ActionToDiscriminatedDto } from "./p0-action-runtime";

const action = (kind: string, config: Record<string, unknown>): MicroflowAction => ({
  id: `action-${kind}`,
  kind,
  officialType: `Microflows$${kind}`,
  ...config,
} as MicroflowAction);

describe("tryMapP0ActionToDiscriminatedDto", () => {
  it("maps filterList aliases to the runtime DTO contract", () => {
    const dto = tryMapP0ActionToDiscriminatedDto(action("filterList", {
      listVariableName: "sortedNumbers",
      outputListVariableName: "positiveNumbers",
      filterExpression: "$item > 2",
      itemVariableName: "item",
    }));

    expect(dto).toMatchObject({
      actionId: "action-filterList",
      actionKind: "filterList",
      supportLevel: "supported",
      config: {
        sourceListVariableName: "sortedNumbers",
        outputVariableName: "positiveNumbers",
        outputListVariableName: "positiveNumbers",
        conditionExpression: "$item > 2",
        filterExpression: "$item > 2",
        itemVariableName: "item",
      },
    });
  });

  it("maps sortList output aliases to the runtime DTO contract", () => {
    const dto = tryMapP0ActionToDiscriminatedDto(action("sortList", {
      sourceListVariableName: "workList",
      outputListVariableName: "sortedNumbers",
      direction: "asc",
    }));

    expect(dto).toMatchObject({
      actionId: "action-sortList",
      actionKind: "sortList",
      supportLevel: "supported",
      config: {
        sourceListVariableName: "workList",
        outputVariableName: "sortedNumbers",
        outputListVariableName: "sortedNumbers",
        direction: "asc",
      },
    });
  });

  it("maps cast aliases to both legacy and runtime field names", () => {
    const dto = tryMapP0ActionToDiscriminatedDto(action("cast", {
      sourceObjectVariableName: "student",
      targetEntityQualifiedName: "Sales.Member",
      outputVariableName: "member",
      castMode: "strict",
    }));

    expect(dto).toMatchObject({
      actionId: "action-cast",
      actionKind: "cast",
      supportLevel: "supported",
      config: {
        sourceVariable: "student",
        sourceObjectVariableName: "student",
        targetEntity: "Sales.Member",
        targetEntityQualifiedName: "Sales.Member",
        outputVariable: "member",
        outputVariableName: "member",
        castMode: "strict",
      },
    });
  });
});
