import type { MicroflowSchema } from "../types";

/**
 * 将历史 createVariable 节点的 action 自动映射为 declareLocalVariable 格式。
 * 调用时机：编辑器加载 schema 后，保存前。
 * 该迁移是单向的，不可逆。
 */
export function migrateCreateVariableToDeclareLocalVariable(schema: MicroflowSchema): MicroflowSchema {
  if (!schema?.objectCollection?.objects) {
    return schema;
  }

  const migrateObject = (obj: Record<string, unknown>): Record<string, unknown> => {
    if (obj.kind === "actionActivity" || obj.type === "actionActivity") {
      const action = obj.action as Record<string, unknown> | undefined;
      if (action && action.kind === "createVariable") {
        const initialValue = action.initialValue as { raw?: string } | string | undefined;
        const expressionRaw = typeof initialValue === "string"
          ? initialValue
          : initialValue?.raw ?? "";
        const migratedAction = {
          ...action,
          kind: "declareLocalVariable",
          officialType: "Microflows$DeclareLocalVariableAction",
          scope: "local",
          source: expressionRaw ? "expression" : "empty",
          expression: expressionRaw ? (typeof initialValue === "object" ? initialValue : { raw: expressionRaw }) : undefined,
          initialValue: undefined
        };
        return { ...obj, action: migratedAction };
      }
    }

    // 递归处理 objectCollection
    if (obj.objectCollection && typeof obj.objectCollection === "object") {
      const collection = obj.objectCollection as Record<string, unknown>;
      if (Array.isArray(collection.objects)) {
        return {
          ...obj,
          objectCollection: {
            ...collection,
            objects: collection.objects.map((child: unknown) =>
              migrateObject(child as Record<string, unknown>)
            )
          }
        };
      }
    }

    return obj;
  };

  const objects = schema.objectCollection.objects;
  if (!Array.isArray(objects)) {
    return schema;
  }

  const migratedObjects = objects.map((obj: unknown) =>
    migrateObject(obj as Record<string, unknown>)
  );

  return {
    ...schema,
    objectCollection: {
      ...schema.objectCollection,
      objects: migratedObjects as unknown as MicroflowSchema["objectCollection"]["objects"]
    }
  };
}
