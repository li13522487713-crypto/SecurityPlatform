import { getEntityByQualifiedName, type MicroflowMetadataCatalog } from "../../metadata";
import { microflowActionRegistryByKind } from "../../node-registry/action-registry";
import type {
  MicroflowAction,
  MicroflowActionActivity,
  MicroflowAggregateListAction,
  MicroflowCallMicroflowAction,
  MicroflowChangeListAction,
  MicroflowChangeMembersAction,
  MicroflowChangeVariableAction,
  MicroflowCommitAction,
  MicroflowCreateListAction,
  MicroflowCreateObjectAction,
  MicroflowCreateVariableAction,
  MicroflowDeleteAction,
  MicroflowListOperationAction,
  MicroflowRestCallAction,
  MicroflowRetrieveAction,
  MicroflowRollbackAction,
} from "../../schema";

function firstNonEmpty(...values: Array<string | undefined | null>): string | undefined {
  for (const value of values) {
    const trimmed = value?.trim();
    if (trimmed) {
      return trimmed;
    }
  }
  return undefined;
}

function shortQualifiedName(qualifiedName?: string): string | undefined {
  const trimmed = qualifiedName?.trim();
  if (!trimmed) {
    return undefined;
  }
  const parts = trimmed.split(".");
  return parts[parts.length - 1] || trimmed;
}

function entityLabel(catalog: MicroflowMetadataCatalog | undefined, qualifiedName?: string): string | undefined {
  const entity = catalog ? getEntityByQualifiedName(catalog, qualifiedName) : undefined;
  return firstNonEmpty(entity?.name, shortQualifiedName(qualifiedName));
}

function fallbackActionTitle(action: MicroflowAction): string {
  const registryEntry = microflowActionRegistryByKind.get(action.kind);
  return firstNonEmpty(registryEntry?.title, registryEntry?.defaultCaption, action.caption, action.kind) ?? action.kind;
}

function labelWithFallback(base: string, subject?: string): string {
  return subject ? `${base} ${subject}` : base;
}

function normalizeUrlPath(raw?: string): string | undefined {
  const trimmed = raw?.trim().replace(/^['"]|['"]$/g, "");
  if (!trimmed) {
    return undefined;
  }
  try {
    const url = new URL(trimmed);
    return `${url.pathname}${url.search}` || trimmed;
  } catch {
    return trimmed;
  }
}

function deriveRetrieveCaption(action: MicroflowRetrieveAction, catalog?: MicroflowMetadataCatalog): string {
  if (action.retrieveSource.kind === "database") {
    return labelWithFallback("Retrieve", entityLabel(catalog, action.retrieveSource.entityQualifiedName)) || "Retrieve Object(s)";
  }
  return labelWithFallback("Retrieve", shortQualifiedName(action.retrieveSource.associationQualifiedName)) || "Retrieve Object(s)";
}

function deriveCreateObjectCaption(action: MicroflowCreateObjectAction, catalog?: MicroflowMetadataCatalog): string {
  return labelWithFallback("Create", entityLabel(catalog, action.entityQualifiedName)) || "Create Object";
}

function deriveChangeMembersCaption(action: MicroflowChangeMembersAction): string {
  return labelWithFallback("Change", firstNonEmpty(action.changeVariableName, "Object"));
}

function deriveSimpleObjectActionCaption(
  prefix: string,
  action: MicroflowCommitAction | MicroflowDeleteAction | MicroflowRollbackAction,
): string {
  return labelWithFallback(prefix, firstNonEmpty(action.objectOrListVariableName, "Object"));
}

function deriveCallMicroflowCaption(action: MicroflowCallMicroflowAction): string {
  return labelWithFallback(
    "Call",
    firstNonEmpty(
      action.targetMicroflowDisplayName,
      action.targetMicroflowName,
      shortQualifiedName(action.targetMicroflowQualifiedName),
      action.targetMicroflowId,
      "Microflow",
    ),
  );
}

function deriveCreateVariableCaption(action: MicroflowCreateVariableAction): string {
  return labelWithFallback("Create", firstNonEmpty(action.variableName, "Variable"));
}

function deriveChangeVariableCaption(action: MicroflowChangeVariableAction): string {
  return labelWithFallback("Change", firstNonEmpty(action.targetVariableName, "Variable"));
}

function deriveCreateListCaption(action: MicroflowCreateListAction): string {
  return labelWithFallback("Create", firstNonEmpty(action.outputListVariableName, action.listVariableName, "List"));
}

function deriveChangeListCaption(action: MicroflowChangeListAction): string {
  return labelWithFallback("Change", firstNonEmpty(action.targetListVariableName, "List"));
}

function deriveAggregateListCaption(action: MicroflowAggregateListAction): string {
  const prefix = action.aggregateFunction.charAt(0).toUpperCase() + action.aggregateFunction.slice(1);
  return labelWithFallback(prefix, firstNonEmpty(action.listVariableName, action.sourceListVariableName, "List"));
}

function deriveListOperationCaption(action: MicroflowListOperationAction): string {
  const prefix = action.operation.charAt(0).toUpperCase() + action.operation.slice(1);
  return labelWithFallback(prefix, firstNonEmpty(action.leftListVariableName, action.sourceListVariableName, "List"));
}

function deriveRestCallCaption(action: MicroflowRestCallAction): string {
  const path = normalizeUrlPath(action.request.urlExpression?.raw);
  return path ? `${action.request.method} ${path}` : fallbackActionTitle(action);
}

function deriveGenericCaption(action: MicroflowAction): string {
  const anyAction = action as Record<string, unknown>;
  if (action.kind === "cast") {
    const sourceVariable = firstNonEmpty(anyAction.sourceVariable as string | undefined, anyAction.sourceObjectVariableName as string | undefined);
    const targetEntity = firstNonEmpty(shortQualifiedName(anyAction.targetEntity as string | undefined), shortQualifiedName(anyAction.targetEntityQualifiedName as string | undefined));
    return labelWithFallback("Cast", firstNonEmpty(sourceVariable && targetEntity ? `${sourceVariable} to ${targetEntity}` : undefined, sourceVariable, targetEntity, "Object"));
  }
  if (action.kind === "logMessage") {
    return labelWithFallback("Log", firstNonEmpty(anyAction.logNodeName as string | undefined, "Message"));
  }
  if (action.kind === "counter") {
    return labelWithFallback("Counter", firstNonEmpty(anyAction.metricName as string | undefined));
  }
  if (action.kind === "incrementCounter") {
    return labelWithFallback("Increment", firstNonEmpty(anyAction.metricName as string | undefined, "Counter"));
  }
  if (action.kind === "gauge") {
    return labelWithFallback("Gauge", firstNonEmpty(anyAction.metricName as string | undefined));
  }
  if (action.kind === "filterList") {
    return labelWithFallback("Filter", firstNonEmpty(anyAction.listVariableName as string | undefined, anyAction.sourceListVariableName as string | undefined, "List"));
  }
  if (action.kind === "sortList") {
    return labelWithFallback("Sort", firstNonEmpty(anyAction.listVariableName as string | undefined, anyAction.sourceListVariableName as string | undefined, "List"));
  }
  return fallbackActionTitle(action);
}

export function deriveAutoGeneratedCaption(action: MicroflowAction, catalog?: MicroflowMetadataCatalog): string {
  switch (action.kind) {
    case "retrieve":
      return deriveRetrieveCaption(action, catalog);
    case "createObject":
      return deriveCreateObjectCaption(action, catalog);
    case "changeMembers":
      return deriveChangeMembersCaption(action);
    case "commit":
      return deriveSimpleObjectActionCaption("Commit", action);
    case "delete":
      return deriveSimpleObjectActionCaption("Delete", action);
    case "rollback":
      return deriveSimpleObjectActionCaption("Rollback", action);
    case "callMicroflow":
      return deriveCallMicroflowCaption(action);
    case "createVariable":
      return deriveCreateVariableCaption(action);
    case "changeVariable":
      return deriveChangeVariableCaption(action);
    case "createList":
      return deriveCreateListCaption(action);
    case "changeList":
      return deriveChangeListCaption(action);
    case "aggregateList":
      return deriveAggregateListCaption(action);
    case "listOperation":
      return deriveListOperationCaption(action);
    case "restCall":
      return deriveRestCallCaption(action);
    default:
      return deriveGenericCaption(action);
  }
}

export function syncActionActivityCaption(
  activity: MicroflowActionActivity,
  catalog?: MicroflowMetadataCatalog,
): MicroflowActionActivity {
  const nextCaption = activity.autoGenerateCaption
    ? deriveAutoGeneratedCaption(activity.action, catalog)
    : firstNonEmpty(activity.caption, activity.action.caption, fallbackActionTitle(activity.action)) ?? fallbackActionTitle(activity.action);
  if (activity.caption === nextCaption && activity.action.caption === nextCaption) {
    return activity;
  }
  return {
    ...activity,
    caption: nextCaption,
    action: {
      ...activity.action,
      caption: nextCaption,
    },
  };
}
