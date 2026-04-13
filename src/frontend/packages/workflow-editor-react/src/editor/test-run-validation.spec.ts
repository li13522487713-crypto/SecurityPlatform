import { describe, expect, it } from "vitest";
import { validateTestRunPayload } from "./test-run-validation";

describe("validateTestRunPayload", () => {
  it("接受普通工作流的空输入对象", () => {
    expect(validateTestRunPayload("", "workflow")).toEqual({
      payload: {},
      issues: []
    });
  });

  it("会拦截非法 JSON", () => {
    expect(validateTestRunPayload("{", "workflow").issues).toContain("invalidJson");
  });

  it("会拦截非对象类型输入", () => {
    expect(validateTestRunPayload("[]", "workflow").issues).toContain("invalidPayload");
  });

  it("会校验 chatflow 的必填字段", () => {
    expect(validateTestRunPayload("{}", "chatflow").issues).toEqual(["userInputRequired", "conversationNameRequired"]);
  });

  it("会校验 chatflow 会话名长度", () => {
    const result = validateTestRunPayload(
      JSON.stringify({
        USER_INPUT: "hello",
        CONVERSATION_NAME: "a".repeat(101)
      }),
      "chatflow"
    );

    expect(result.issues).toEqual(["conversationNameTooLong"]);
  });
});
