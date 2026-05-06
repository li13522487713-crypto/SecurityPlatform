import { describe, expect, it, vi } from "vitest";

import {
  consumeRuntimeCommand,
  createRuntimeCommandConsoleEntry,
  isSupportedClientRuntimeCommand,
  parseRuntimeCommandPayload,
  type MicroflowRuntimeCommand,
} from "./runtime-command-consumer";

function command(kind: string, payloadJson?: string): MicroflowRuntimeCommand {
  return {
    commandKind: kind,
    payloadJson,
    sourceObjectId: "node-1",
    sourceActionId: "action-1",
  };
}

describe("runtime-command-consumer", () => {
  it("parses payload json", () => {
    expect(parseRuntimeCommandPayload("{\"pageId\":\"home\"}")?.pageId).toBe("home");
    expect(parseRuntimeCommandPayload("invalid")).toBeUndefined();
  });

  it("identifies supported client commands", () => {
    expect(isSupportedClientRuntimeCommand("showPage")).toBe(true);
    expect(isSupportedClientRuntimeCommand("openTaskPage")).toBe(true);
    expect(isSupportedClientRuntimeCommand("downloadFile")).toBe(true);
    expect(isSupportedClientRuntimeCommand("closePage")).toBe(true);
    expect(isSupportedClientRuntimeCommand("showMessage")).toBe(false);
  });

  it("consumes showPage/openTaskPage and resolves targets", () => {
    const openPage = vi.fn(() => true);
    const showPage = consumeRuntimeCommand(
      command("showPage"),
      { pageId: "dashboard" },
      { openPage },
    );
    const openTaskPage = consumeRuntimeCommand(
      command("openTaskPage"),
      { workflowTaskId: "task-99" },
      { openPage },
    );

    expect(showPage.handled).toBe(true);
    expect(showPage.target).toBe("/pages/dashboard");
    expect(openTaskPage.handled).toBe(true);
    expect(openTaskPage.target).toBe("/tasks/task-99");
    expect(openPage).toHaveBeenCalledTimes(2);
  });

  it("consumes downloadFile and closePage", () => {
    const downloadFile = vi.fn(() => true);
    const closePage = vi.fn(() => false);

    const download = consumeRuntimeCommand(
      command("downloadFile"),
      { downloadUrl: "/files/report.pdf", fileName: "report.pdf" },
      { downloadFile },
    );
    const close = consumeRuntimeCommand(
      command("closePage"),
      {},
      { closePage },
    );

    expect(download.handled).toBe(true);
    expect(download.target).toBe("/files/report.pdf");
    expect(close.handled).toBe(false);
    expect(close.severity).toBe("warning");
  });

  it("creates console entry for consumed runtime command", () => {
    const result = consumeRuntimeCommand(
      command("showPage"),
      { pageId: "home" },
      { openPage: () => true },
    );
    const entry = createRuntimeCommandConsoleEntry(command("showPage"), result, "run-1", "2026-05-06T00:00:00.000Z");

    expect(entry.runId).toBe("run-1");
    expect(entry.commandKind).toBe("showPage");
    expect(entry.handled).toBe(true);
    expect(entry.target).toBe("/pages/home");
  });
});
