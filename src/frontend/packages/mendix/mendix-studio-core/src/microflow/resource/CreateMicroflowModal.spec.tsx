// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import React from "react";

import { CreateMicroflowModal } from "./CreateMicroflowModal";
import { MicroflowApiException } from "../adapter/http/microflow-api-error";

const { toastMock } = vi.hoisted(() => ({
  toastMock: {
    success: vi.fn(),
    warning: vi.fn(),
    error: vi.fn(),
  }
}));

vi.mock("@douyinfe/semi-ui", async () => {
  const react = await import("react");
  const FormInput = ({ label, value, onChange, disabled, placeholder }: any) => (
    <label>
      {label}
      <input
        aria-label={label}
        value={value ?? ""}
        disabled={disabled}
        placeholder={placeholder}
        onChange={event => onChange?.(event.target.value)}
      />
    </label>
  );
  const FormSelect = ({ label, value, onChange, optionList, placeholder, disabled }: any) => (
    <label>
      {label}
      <select
        aria-label={label}
        value={value ?? ""}
        disabled={disabled}
        onChange={event => onChange?.(event.target.value)}
      >
        <option value="">{placeholder ?? ""}</option>
        {(optionList ?? []).map((item: any) => <option key={item.value} value={item.value}>{item.label}</option>)}
      </select>
    </label>
  );
  return {
    Toast: toastMock,
    Modal: ({ visible, children, onOk, onCancel, title }: any) => visible ? (
      <div>
        <h1>{title}</h1>
        {children}
        <button type="button" onClick={onOk}>创建</button>
        <button type="button" onClick={onCancel}>关闭</button>
      </div>
    ) : null,
    Form: Object.assign(({ children }: any) => <div>{children}</div>, {
      Section: ({ children }: any) => <section>{children}</section>,
      Input: FormInput,
      TextArea: FormInput,
      Select: FormSelect,
    }),
    Input: ({ value, onChange, placeholder }: any) => <input value={value ?? ""} placeholder={placeholder} onChange={event => onChange?.(event.target.value)} />,
    Select: ({ value, onChange, optionList }: any) => (
      <select value={value ?? ""} onChange={event => onChange?.(event.target.value)}>
        {(optionList ?? []).map((item: any) => <option key={item.value} value={item.value}>{item.label}</option>)}
      </select>
    ),
    Button: ({ children, onClick }: any) => <button type="button" onClick={onClick}>{children}</button>,
    Checkbox: ({ checked, onChange, children }: any) => (
      <label>
        <input type="checkbox" checked={checked} onChange={event => onChange?.({ target: { checked: event.target.checked } })} />
        {children}
      </label>
    ),
    Space: ({ children }: any) => <div>{children}</div>,
    Typography: { Text: ({ children }: any) => <p>{children}</p> },
  };
});

function renderModal(input?: Partial<React.ComponentProps<typeof CreateMicroflowModal>>) {
  const onClose = vi.fn();
  const onCreated = vi.fn();
  const onSubmit = vi.fn();
  render(
    <CreateMicroflowModal
      visible
      existingResources={[]}
      defaultModuleId="sales"
      moduleLocked={false}
      onClose={onClose}
      onCreated={onCreated}
      onSubmit={onSubmit}
      {...input}
    />
  );
  return { onClose, onCreated, onSubmit };
}

function fillMinimumValidInput() {
  fireEvent.change(screen.getAllByLabelText("Name")[0], { target: { value: "OrderCreate" } });
}

function clickSubmit() {
  fireEvent.click(screen.getAllByText("创建")[0]);
}

describe("CreateMicroflowModal", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    cleanup();
  });

  it("onSubmit reject 时不关闭弹窗", async () => {
    const { onClose, onSubmit } = renderModal();
    onSubmit.mockRejectedValueOnce(new MicroflowApiException("冲突", { status: 409, apiError: { code: "MICROFLOW_NAME_DUPLICATED", message: "dup", httpStatus: 409 } }));
    fillMinimumValidInput();

    clickSubmit();

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    expect(onClose).not.toHaveBeenCalled();
    expect(screen.getByText("status: 409")).toBeTruthy();
  });

  it("onSubmit reject 后可恢复 loading 并可再次提交", async () => {
    const { onSubmit } = renderModal();
    onSubmit.mockRejectedValueOnce(new Error("fail"));
    onSubmit.mockResolvedValueOnce({ id: "id-1", name: "OrderCreate" });
    fillMinimumValidInput();

    clickSubmit();
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));

    clickSubmit();
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(2));
  });

  it("reject 时展示 message/code/traceId", async () => {
    const { onSubmit } = renderModal();
    onSubmit.mockRejectedValueOnce(new MicroflowApiException("后端异常", {
      status: 500,
      traceId: "trace-500",
      apiError: {
        code: "MICROFLOW_UNKNOWN_ERROR",
        message: "后端异常",
        httpStatus: 500,
        traceId: "trace-500",
      }
    }));
    fillMinimumValidInput();
    clickSubmit();

    await waitFor(() => {
      expect(screen.getByText("微流服务异常，请联系管理员。")).toBeTruthy();
      expect(screen.getByText("code: MICROFLOW_UNKNOWN_ERROR")).toBeTruthy();
      expect(screen.getByText("traceId: trace-500")).toBeTruthy();
    });
  });

  it("409 duplicated 时 Name 字段显示错误", async () => {
    const { onSubmit } = renderModal();
    onSubmit.mockRejectedValueOnce(new MicroflowApiException("冲突", {
      status: 409,
      apiError: {
        code: "MICROFLOW_NAME_DUPLICATED",
        message: "dup",
        httpStatus: 409,
      }
    }));
    fillMinimumValidInput();
    clickSubmit();

    await waitFor(() => expect(screen.getAllByText("同名微流已存在。").length).toBeGreaterThan(0));
  });

  it("422 fieldErrors 时展示对应字段错误", async () => {
    const { onSubmit } = renderModal();
    onSubmit.mockRejectedValueOnce(new MicroflowApiException("校验失败", {
      status: 422,
      apiError: {
        code: "MICROFLOW_VALIDATION_FAILED",
        message: "校验失败",
        httpStatus: 422,
        fieldErrors: [{ fieldPath: "input.name", code: "INVALID_FORMAT", message: "name 格式非法。" }],
      }
    }));
    fillMinimumValidInput();
    clickSubmit();

    await waitFor(() => expect(screen.getByText("name 格式非法。")).toBeTruthy());
  });

  it("invalid name _abc 时前端阻止提交", async () => {
    const { onSubmit } = renderModal();
    fireEvent.change(screen.getAllByLabelText("Name")[0], { target: { value: "_abc" } });
    clickSubmit();

    await waitFor(() => expect(toastMock.warning).toHaveBeenCalled());
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("missing moduleId 时前端阻止提交", async () => {
    const { onSubmit } = renderModal({ defaultModuleId: undefined, initialModuleId: undefined });
    fillMinimumValidInput();
    fireEvent.click(screen.getByText("创建"));

    await waitFor(() => expect(screen.getByText("缺少模块上下文，无法创建微流。")).toBeTruthy());
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("double click submit 时只提交一次", async () => {
    const { onSubmit } = renderModal();
    let resolveSubmit: ((value: any) => void) | undefined;
    onSubmit.mockImplementation(
      () => new Promise(resolve => {
        resolveSubmit = resolve;
      })
    );
    fillMinimumValidInput();

    clickSubmit();
    clickSubmit();

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    resolveSubmit?.({ id: "id-2", name: "OrderCreate" });
  });
});
