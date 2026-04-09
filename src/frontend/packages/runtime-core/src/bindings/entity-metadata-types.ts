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
