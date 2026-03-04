import type { LfFormField } from '@/types/approval-definition';

type JsonObject = Record<string, unknown>;

const normalizeValueType = (schema: JsonObject): string => {
  const type = typeof schema.type === 'string' ? schema.type : '';
  if (['number', 'integer'].includes(type)) {
    return 'Number';
  }
  if (type === 'boolean') {
    return 'Boolean';
  }
  if (['array', 'object'].includes(type)) {
    return 'Json';
  }
  return 'String';
};

const inferFieldType = (schema: JsonObject): string => {
  const amisType = typeof schema.type === 'string' ? schema.type : '';
  return amisType || 'input-text';
};

export const extractAmisFields = (schema: unknown): LfFormField[] => {
  const fields: LfFormField[] = [];
  const visited = new Set<string>();

  const traverse = (node: unknown) => {
    if (!node || typeof node !== 'object') {
      return;
    }

    const schemaNode = node as JsonObject;
    const name = typeof schemaNode.name === 'string' ? schemaNode.name : '';
    const label = typeof schemaNode.label === 'string' ? schemaNode.label : name;
    if (name && !visited.has(name)) {
      visited.add(name);
      fields.push({
        fieldId: name,
        fieldName: label || name,
        fieldType: inferFieldType(schemaNode),
        valueType: normalizeValueType(schemaNode),
        options: Array.isArray(schemaNode.options)
          ? (schemaNode.options as Array<{ key?: string; value?: string; label?: string }>)
              .map((item) => ({
                key: String(item?.value ?? item?.key ?? ''),
                value: String(item?.label ?? item?.value ?? item?.key ?? '')
              }))
              .filter((item) => item.key)
          : []
      });
    }

    const childKeys = ['body', 'controls', 'tabs', 'columns', 'items', 'children', 'actions'];
    childKeys.forEach((key) => {
      const child = schemaNode[key];
      if (Array.isArray(child)) {
        child.forEach(traverse);
      } else if (child && typeof child === 'object') {
        traverse(child);
      }
    });
  };

  traverse(schema);
  return fields;
};
