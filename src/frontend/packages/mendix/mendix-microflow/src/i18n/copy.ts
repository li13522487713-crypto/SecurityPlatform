export type MendixMicroflowLocale = "zh-CN" | "en-US";

export interface MendixMicroflowCopy {
  readonly canvasToolbar: {
    /** Short label for the pan / hand tool toggle (tooltip carries shortcuts). */
    readonly panTool: string;
    readonly panToolTooltip: string;
  };
  readonly testRun: {
    readonly title: string;
    readonly dirtyTag: string;
    readonly savedTag: string;
    readonly validationErrorsTag: string;
    readonly validationReadyTag: string;
    readonly runDescription: string;
    readonly noInputParameters: string;
    readonly runtimeOptions: string;
    readonly allowRealHttp: string;
    readonly allowRealHttpHint: string;
    readonly maxSteps: string;
    readonly cancel: string;
    readonly run: string;
    readonly saveAndRun: string;
    readonly numberPlaceholder: string;
    readonly dateTimePlaceholder: string;
    readonly unsupportedInputPlaceholder: string;
    readonly resultPlaceholder: string;
    readonly childRunsTag: string;
    readonly traceFramesTag: string;
    readonly logsTag: string;
    readonly callDepthTag: string;
    readonly runIdTag: string;
    readonly callStackPrefix: string;
    readonly samplesTitle: string;
    readonly sampleNamePlaceholder: string;
    readonly expectedResultPlaceholder: string;
    readonly saveSample: string;
    readonly runAllSamples: string;
    readonly noSamples: string;
    readonly loadSample: string;
    readonly runSample: string;
    readonly expectedLabel: string;
    readonly actualLabel: string;
    readonly previousLabel: string;
    readonly matchTag: string;
    readonly mismatchTag: string;
    readonly invalidExpectedResult: string;
    readonly copyTrace: string;
    readonly traceCopied: string;
  };
}

const zhCN: MendixMicroflowCopy = {
  canvasToolbar: {
    panTool: "平移",
    panToolTooltip: "拖动画布（空白处按住拖动）。按住空格拖动或鼠标中键拖动也可平移。",
  },
  testRun: {
    title: "运行微流",
    dirtyTag: "未保存 - Save & Run",
    savedTag: "已保存草稿",
    validationErrorsTag: "{count} 个校验错误",
    validationReadyTag: "校验门禁通过",
    runDescription:
      "运行会先执行本地与后端校验；dirty 状态先 Save & Run，保存成功后调用真实后端 POST /api/v1/microflows/{id}/test-run，并提交类型转换后的输入参数。",
    noInputParameters: "当前微流没有输入参数。",
    runtimeOptions: "运行选项",
    allowRealHttp: "allowRealHttp",
    allowRealHttpHint: "默认关闭，后端策略不允许时会真实返回错误。",
    maxSteps: "maxSteps",
    cancel: "取消",
    run: "运行",
    saveAndRun: "保存并运行",
    numberPlaceholder: "输入数字",
    dateTimePlaceholder: "2026-04-28T10:00:00Z",
    unsupportedInputPlaceholder: "不可作为运行输入",
    resultPlaceholder: "运行结果会显示在这里，并同步到底部 Debug 面板。",
    childRunsTag: "{count} 个子运行",
    traceFramesTag: "{count} 帧",
    logsTag: "{count} 日志",
    callDepthTag: "深度 {depth}",
    runIdTag: "runId {id}",
    callStackPrefix: "调用栈",
    samplesTitle: "测试样例",
    sampleNamePlaceholder: "样例名称",
    expectedResultPlaceholder: "期望结果 JSON，可留空",
    saveSample: "保存样例",
    runAllSamples: "运行全部样例",
    noSamples: "还没有保存测试样例。",
    loadSample: "载入",
    runSample: "运行",
    expectedLabel: "期望",
    actualLabel: "实际",
    previousLabel: "上次",
    matchTag: "匹配",
    mismatchTag: "不匹配",
    invalidExpectedResult: "期望结果必须是合法 JSON。",
    copyTrace: "复制 Debug trace",
    traceCopied: "Debug trace 已复制",
  },
};

const enUS: MendixMicroflowCopy = {
  canvasToolbar: {
    panTool: "Pan",
    panToolTooltip: "Drag the canvas on empty space. Hold Space and drag, or drag with the middle mouse button.",
  },
  testRun: {
    title: "Run Microflow",
    dirtyTag: "dirty - Save & Run",
    savedTag: "saved draft",
    validationErrorsTag: "{count} validation errors",
    validationReadyTag: "validation gate ready",
    runDescription:
      "Run performs local and backend validation first. When dirty, it executes Save & Run, then calls backend POST /api/v1/microflows/{id}/test-run with typed parameters.",
    noInputParameters: "This microflow has no input parameters.",
    runtimeOptions: "Runtime options",
    allowRealHttp: "allowRealHttp",
    allowRealHttpHint: "Disabled by default. Backend policy returns a real error when not allowed.",
    maxSteps: "maxSteps",
    cancel: "Cancel",
    run: "Run",
    saveAndRun: "Save & Run",
    numberPlaceholder: "Enter a number",
    dateTimePlaceholder: "2026-04-28T10:00:00Z",
    unsupportedInputPlaceholder: "Cannot be used as runtime input",
    resultPlaceholder: "Run results will appear here and sync to the bottom Debug panel.",
    childRunsTag: "{count} child runs",
    traceFramesTag: "{count} frames",
    logsTag: "{count} logs",
    callDepthTag: "depth {depth}",
    runIdTag: "runId {id}",
    callStackPrefix: "callStack",
    samplesTitle: "Test samples",
    sampleNamePlaceholder: "Sample name",
    expectedResultPlaceholder: "Expected result JSON, optional",
    saveSample: "Save sample",
    runAllSamples: "Run all samples",
    noSamples: "No saved test samples yet.",
    loadSample: "Load",
    runSample: "Run",
    expectedLabel: "Expected",
    actualLabel: "Actual",
    previousLabel: "Previous",
    matchTag: "match",
    mismatchTag: "mismatch",
    invalidExpectedResult: "Expected result must be valid JSON.",
    copyTrace: "Copy Debug trace",
    traceCopied: "Debug trace copied",
  },
};

function readLocaleFromStorage(): MendixMicroflowLocale {
  if (typeof window === "undefined") {
    return "zh-CN";
  }
  const value = window.localStorage.getItem("atlas_locale");
  return value === "en-US" ? "en-US" : "zh-CN";
}

export function resolveMendixMicroflowLocale(locale?: string): MendixMicroflowLocale {
  if (locale === "en-US" || locale === "zh-CN") {
    return locale;
  }
  return readLocaleFromStorage();
}

export function getMendixMicroflowCopy(locale?: string): MendixMicroflowCopy {
  return resolveMendixMicroflowLocale(locale) === "en-US" ? enUS : zhCN;
}

export function formatMendixMicroflowTemplate(template: string, values: Record<string, string | number>): string {
  return template.replace(/\{(\w+)\}/g, (_all, key: string) => String(values[key] ?? ""));
}
