import { describe, expect, it } from "vitest";
import {
  CHATFLOW_ROLE_CONFIG_KEY,
  ensureChatflowGlobals,
  patchChatflowRoleConfig,
  readChatflowRoleConfig,
  validateChatflowRoleConfig
} from "./chatflow-role-config";

describe("chatflow role config helpers", () => {
  it("injects default role config into empty globals", () => {
    const globals = ensureChatflowGlobals({});

    expect(globals).toHaveProperty(CHATFLOW_ROLE_CONFIG_KEY);
    expect(readChatflowRoleConfig(globals)).toEqual({
      roleName: "",
      roleDescription: "",
      avatarLabel: "",
      openingText: "",
      openingQuestions: [],
      showAllOpeningQuestions: false
    });
  });

  it("normalizes stored questions and boolean values", () => {
    const normalized = readChatflowRoleConfig({
      [CHATFLOW_ROLE_CONFIG_KEY]: {
        roleName: "助手",
        openingQuestions: [" 你好 ", "", "如何开始？", 1],
        showAllOpeningQuestions: true
      }
    });

    expect(normalized.roleName).toBe("助手");
    expect(normalized.openingQuestions).toEqual(["你好", "", "如何开始？", ""]);
    expect(normalized.showAllOpeningQuestions).toBe(true);
  });

  it("patches only the requested fields", () => {
    const next = patchChatflowRoleConfig(
      {
        [CHATFLOW_ROLE_CONFIG_KEY]: {
          roleName: "客服助手",
          roleDescription: "负责接待",
          avatarLabel: "客",
          openingText: "你好",
          openingQuestions: ["怎么下单"],
          showAllOpeningQuestions: false
        }
      },
      {
        openingText: "欢迎来到工作台",
        openingQuestions: ["如何创建工单", "如何查看进度"]
      }
    );

    expect(readChatflowRoleConfig(next)).toEqual({
      roleName: "客服助手",
      roleDescription: "负责接待",
      avatarLabel: "客",
      openingText: "欢迎来到工作台",
      openingQuestions: ["如何创建工单", "如何查看进度"],
      showAllOpeningQuestions: false
    });
  });

  it("reports empty or duplicate onboarding questions", () => {
    const issues = validateChatflowRoleConfig({
      roleName: "客服助手",
      roleDescription: "",
      avatarLabel: "客",
      openingText: "",
      openingQuestions: ["如何报修", "", "如何报修"],
      showAllOpeningQuestions: false
    });

    expect(issues).toContain("预置问题不能为空。");
    expect(issues).toContain("预置问题不能重复。");
  });

  it("requires a role name", () => {
    const issues = validateChatflowRoleConfig({
      roleName: "",
      roleDescription: "",
      avatarLabel: "",
      openingText: "",
      openingQuestions: [],
      showAllOpeningQuestions: false
    });

    expect(issues).toContain("角色名称不能为空。");
  });
});
