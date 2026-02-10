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

export interface DynamicTableListItem {
  id: string;
  tableKey: string;
  displayName: string;
  description?: string | null;
  dbType: DynamicDbType;
  status: "Draft" | "Active" | "Disabled";
  createdAt: string;
  createdBy: number;
  approvalFlowDefinitionId?: number | null;
  approvalStatusField?: string | null;
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
}

export interface DynamicIndexDefinition {
  name: string;
  isUnique: boolean;
  fields: string[];
}

export interface DynamicTableDetail extends DynamicTableListItem {
  updatedAt: string;
  updatedBy: number;
  fields: DynamicFieldDefinition[];
  indexes: DynamicIndexDefinition[];
}

export interface DynamicTableCreateRequest {
  tableKey: string;
  displayName: string;
  description?: string | null;
  dbType: DynamicDbType;
  fields: DynamicFieldDefinition[];
  indexes: DynamicIndexDefinition[];
}

export interface DynamicTableUpdateRequest {
  displayName: string;
  description?: string | null;
  status: "Draft" | "Active" | "Disabled";
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
