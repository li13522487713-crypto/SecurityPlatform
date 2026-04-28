/**
 * AI 数据库 API 客户端文案（中英）。供纯函数 API 层使用（非 React 组件内 i18n hook）。
 */
export type AiDatabaseMessageKey =
  | "getDatabasesPagedFailed"
  | "getDatabaseDetailFailed"
  | "createDatabaseFailed"
  | "updateDatabaseFailed"
  | "deleteDatabaseFailed"
  | "validateSchemaFailed"
  | "getRecordsFailed"
  | "createRecordFailed"
  | "updateRecordFailed"
  | "deleteRecordFailed"
  | "bulkCreateFailed"
  | "bulkAsyncSubmitFailed"
  | "uploadImportFileFailed"
  | "submitImportFailed";

const zh: Record<AiDatabaseMessageKey, string> = {
  getDatabasesPagedFailed: "查询数据库失败",
  getDatabaseDetailFailed: "查询数据库详情失败",
  createDatabaseFailed: "创建数据库失败",
  updateDatabaseFailed: "更新数据库失败",
  deleteDatabaseFailed: "删除数据库失败",
  validateSchemaFailed: "校验数据库 Schema 失败",
  getRecordsFailed: "查询数据库记录失败",
  createRecordFailed: "创建数据库记录失败",
  updateRecordFailed: "更新数据库记录失败",
  deleteRecordFailed: "删除数据库记录失败",
  bulkCreateFailed: "批量插入数据库记录失败",
  bulkAsyncSubmitFailed: "提交批量异步任务失败",
  uploadImportFileFailed: "上传导入文件失败",
  submitImportFailed: "提交数据库导入任务失败"
};

const en: Record<AiDatabaseMessageKey, string> = {
  getDatabasesPagedFailed: "Failed to list databases",
  getDatabaseDetailFailed: "Failed to load database detail",
  createDatabaseFailed: "Failed to create database",
  updateDatabaseFailed: "Failed to update database",
  deleteDatabaseFailed: "Failed to delete database",
  validateSchemaFailed: "Failed to validate database schema",
  getRecordsFailed: "Failed to list database records",
  createRecordFailed: "Failed to create database record",
  updateRecordFailed: "Failed to update database record",
  deleteRecordFailed: "Failed to delete database record",
  bulkCreateFailed: "Failed to bulk insert database records",
  bulkAsyncSubmitFailed: "Failed to submit bulk insert job",
  uploadImportFileFailed: "Failed to upload import file",
  submitImportFailed: "Failed to submit database import"
};

function resolveLocale(): "zh" | "en" {
  if (typeof window === "undefined" || !window.localStorage) {
    return typeof navigator !== "undefined" && navigator.language.toLowerCase().startsWith("zh") ? "zh" : "en";
  }
  const stored = window.localStorage.getItem("atlas_locale") ?? window.localStorage.getItem("locale");
  if (stored && stored.toLowerCase().startsWith("zh")) {
    return "zh";
  }
  if (stored && stored.toLowerCase().startsWith("en")) {
    return "en";
  }
  return typeof navigator !== "undefined" && navigator.language.toLowerCase().startsWith("zh") ? "zh" : "en";
}

export function aiDatabaseMessage(key: AiDatabaseMessageKey): string {
  return resolveLocale() === "zh" ? zh[key] : en[key];
}
