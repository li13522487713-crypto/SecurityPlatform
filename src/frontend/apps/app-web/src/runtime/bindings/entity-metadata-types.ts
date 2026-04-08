/**
 * Entity metadata 类型定义。
 *
 * 对齐后端 DynamicFieldDefinition，提供字段元数据
 * 供 BindingResolver 和 RuntimeDataService 使用。
 */

export interface EntityFieldMeta {
  fieldName: string;
  displayName: string;
  fieldType: string;
  allowNull: boolean;
  isPrimaryKey: boolean;
  isAutoIncrement: boolean;
  length?: number;
  precision?: number;
  scale?: number;
  defaultValue?: string;
  isUnique: boolean;
  sortOrder: number;
}

export interface EntityMeta {
  tableKey: string;
  displayName: string;
  fields: EntityFieldMeta[];
}

export interface EntityRelation {
  relationName: string;
  sourceTableKey: string;
  targetTableKey: string;
  sourceField: string;
  targetField: string;
  relationType: "one-to-one" | "one-to-many" | "many-to-many";
}
