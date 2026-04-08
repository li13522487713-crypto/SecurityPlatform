export type DynamicDbType = "Sqlite" | "SqlServer" | "MySql" | "PostgreSql";

export type DynamicFieldType =
  | "Int"
  | "Long"
  | "Decimal"
  | "String"
  | "Text"
  | "Bool"
  | "DateTime"
  | "Date";

export type DynamicTableStatus =
  | "Draft"
  | "Active"
  | "Disabled"
  | "HasUnpublishedChanges"
  | "Archived";

export interface DynamicTableListItem {
  id: string;
  tableKey: string;
  displayName: string;
  description?: string | null;
  dbType: DynamicDbType;
  status: DynamicTableStatus;
  createdAt: string;
  createdBy: number;
  approvalFlowDefinitionId?: number | null;
  approvalStatusField?: string | null;
}

export interface DynamicFieldValidationDefinition {
  pattern?: string | null;
  minLength?: number | null;
  maxLength?: number | null;
}

export interface DynamicFieldDefinition {
  name: string;
  displayName?: string | null;
  fieldType: DynamicFieldType;
  length?: number | null;
  precision?: number | null;
  scale?: number | null;
  allowNull: boolean;
  isPrimaryKey: boolean;
  isAutoIncrement: boolean;
  isUnique: boolean;
  defaultValue?: string | null;
  sortOrder: number;
  validation?: DynamicFieldValidationDefinition | null;
}

export interface DynamicIndexDefinition {
  name: string;
  isUnique: boolean;
  fields: string[];
}

export interface DynamicTableFieldSummary {
  name: string;
  displayName?: string | null;
  fieldType: DynamicFieldType;
  allowNull: boolean;
  isPrimaryKey: boolean;
}

export interface DynamicTableSummary {
  id: string;
  appId?: string | null;
  tableKey: string;
  displayName: string;
  description?: string | null;
  dbType: DynamicDbType;
  status: DynamicTableStatus;
  fieldCount: number;
  indexCount: number;
  relationCount: number;
  referenceCount: number;
  approvalFlowDefinitionId?: number | null;
  approvalStatusField?: string | null;
  previewFields: DynamicTableFieldSummary[];
}

export interface DynamicTableCreateRequest {
  tableKey: string;
  displayName: string;
  description?: string | null;
  appId?: string | null;
  dbType: DynamicDbType;
  fields: DynamicFieldDefinition[];
  indexes: DynamicIndexDefinition[];
}

export interface DynamicFieldUpdateDefinition {
  name: string;
  displayName?: string | null;
  length?: number | null;
  precision?: number | null;
  scale?: number | null;
  allowNull?: boolean | null;
  isUnique?: boolean | null;
  defaultValue?: string | null;
  sortOrder?: number | null;
  validation?: DynamicFieldValidationDefinition | null;
}

export interface DynamicTableAlterRequest {
  addFields: DynamicFieldDefinition[];
  updateFields: DynamicFieldUpdateDefinition[];
  removeFields: string[];
}

export interface DynamicTableAlterPreviewResponse {
  tableKey: string;
  operationType: string;
  sqlScripts: string[];
  rollbackHint?: string | null;
}

export interface AdvancedQueryConfig {
  groupOp?: "AND" | "OR";
  rules?: Array<Record<string, unknown>>;
  groups?: AdvancedQueryConfig[];
}

export interface DynamicFieldValueDto {
  field: string;
  valueType: DynamicFieldType;
  stringValue?: string;
  intValue?: number;
  longValue?: number;
  decimalValue?: number;
  boolValue?: boolean;
  dateTimeValue?: string;
  dateValue?: string;
}

export interface DynamicRecordUpsertRequest {
  values: DynamicFieldValueDto[];
}

export interface DynamicRecordQueryRequest {
  pageIndex: number;
  pageSize: number;
  keyword?: string | null;
  sortBy?: string | null;
  sortDesc?: boolean;
  filters?: Array<{ field: string; operator: string; value?: unknown }>;
  advancedQuery?: AdvancedQueryConfig;
}

export interface DynamicRecordDto {
  id: string;
  values: DynamicFieldValueDto[];
}

export interface DynamicColumnDef {
  name: string;
  label: string;
  type: string;
  sortable: boolean;
  searchable: boolean;
  quickEdit: boolean;
}

export interface DynamicRecordListResult {
  items: DynamicRecordDto[];
  total: number;
  pageIndex: number;
  pageSize: number;
  columns: DynamicColumnDef[];
}

export interface DeleteCheckBlocker {
  type: "form" | "page" | "approval" | "relation" | "view" | "etlJob";
  id: string;
  name: string;
  path?: string;
}

export interface DeleteCheckResult {
  canDelete: boolean;
  blockers: DeleteCheckBlocker[];
  warnings: string[];
}

export interface AppScopedDynamicTableListItem {
  tableKey: string;
  displayName: string;
  status?: string;
}
