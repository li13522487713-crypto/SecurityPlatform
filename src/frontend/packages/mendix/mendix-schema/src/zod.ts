import { z } from "zod";

export const RefZod = z.object({
  kind: z.string(),
  id: z.string().min(1),
  moduleId: z.string().optional(),
  name: z.string().optional()
});

export const DataTypeSchemaZod = z.discriminatedUnion("kind", [
  z.object({ kind: z.literal("Boolean") }),
  z.object({ kind: z.literal("String") }),
  z.object({ kind: z.literal("Integer") }),
  z.object({ kind: z.literal("Long") }),
  z.object({
    kind: z.literal("Decimal"),
    precision: z.number().int().positive().optional(),
    scale: z.number().int().nonnegative().optional()
  }),
  z.object({ kind: z.literal("DateTime") }),
  z.object({ kind: z.literal("Enumeration"), enumerationRef: RefZod }),
  z.object({ kind: z.literal("Binary") }),
  z.object({ kind: z.literal("Object"), entityRef: RefZod }),
  z.object({ kind: z.literal("List"), entityRef: RefZod }),
  z.object({ kind: z.literal("Nothing") })
]);

export const ExpressionNodeZod: z.ZodTypeAny = z.lazy(() =>
  z.discriminatedUnion("type", [
    z.object({
      type: z.literal("literal"),
      value: z.union([z.string(), z.number(), z.boolean(), z.null()])
    }),
    z.object({ type: z.literal("variable"), name: z.string() }),
    z.object({
      type: z.literal("path"),
      root: z.string(),
      segments: z.array(z.string())
    }),
    z.object({
      type: z.literal("binary"),
      operator: z.enum(["and", "or", "=", "!=", ">", "<", ">=", "<="]),
      left: ExpressionNodeZod,
      right: ExpressionNodeZod
    }),
    z.object({
      type: z.literal("function"),
      functionName: z.enum(["empty", "contains"]),
      args: z.array(ExpressionNodeZod)
    }),
    z.object({
      type: z.literal("if"),
      condition: ExpressionNodeZod,
      thenNode: ExpressionNodeZod,
      elseNode: ExpressionNodeZod
    }),
    z.object({
      type: z.literal("enum"),
      enumerationName: z.string(),
      value: z.string()
    })
  ])
);

export const ExpressionSchemaZod = z.object({
  source: z.string(),
  ast: ExpressionNodeZod,
  returnType: DataTypeSchemaZod.optional(),
  dependencies: z.array(RefZod),
  validation: z.array(
    z.object({
      code: z.string(),
      message: z.string(),
      severity: z.enum(["error", "warning", "info"])
    })
  )
});

const AttributeSchemaZod = z.object({
  attributeId: z.string(),
  entityId: z.string(),
  name: z.string(),
  caption: z.string().optional(),
  required: z.boolean().optional(),
  defaultValue: z.string().optional(),
  attributeType: z.enum([
    "Boolean",
    "String",
    "Integer",
    "Long",
    "Decimal",
    "DateTime",
    "Enumeration",
    "Binary",
    "AutoNumber"
  ]),
  dataType: DataTypeSchemaZod
});

const EntityAccessRuleSchemaZod = z.object({
  ruleId: z.string(),
  roleRefs: z.array(RefZod),
  xpathConstraint: z.string().optional(),
  memberAccess: z.array(
    z.object({
      attributeRef: RefZod,
      read: z.boolean(),
      write: z.boolean()
    })
  )
});

const EntitySchemaZod = z.object({
  entityId: z.string(),
  moduleId: z.string(),
  name: z.string(),
  caption: z.string().optional(),
  description: z.string().optional(),
  entityType: z.enum(["persistable", "nonPersistable", "external", "view"]),
  generalization: RefZod.optional(),
  attributes: z.array(AttributeSchemaZod),
  associations: z.array(RefZod),
  accessRules: z.array(EntityAccessRuleSchemaZod),
  validationRules: z.array(z.object({ ruleId: z.string(), expression: ExpressionSchemaZod })),
  eventHandlers: z.array(
    z.object({
      event: z.enum(["beforeCommit", "afterCommit"]),
      microflowRef: RefZod
    })
  ),
  systemMembers: z.object({
    storeOwner: z.boolean(),
    storeCreatedDate: z.boolean(),
    storeChangedDate: z.boolean()
  }),
  ui: z
    .object({
      x: z.number().optional(),
      y: z.number().optional(),
      width: z.number().optional(),
      height: z.number().optional(),
      color: z.string().optional()
    })
    .optional()
});

const AssociationSchemaZod = z.object({
  associationId: z.string(),
  moduleId: z.string(),
  name: z.string(),
  fromEntityRef: RefZod,
  toEntityRef: RefZod,
  owner: z.enum(["default", "both"]),
  cardinality: z.enum(["oneToOne", "oneToMany", "manyToMany"])
});

const EnumerationSchemaZod = z.object({
  enumerationId: z.string(),
  moduleId: z.string(),
  name: z.string(),
  values: z.array(
    z.object({
      key: z.string(),
      caption: z.string(),
      color: z.string().optional()
    })
  )
});

const DataSourceSchemaZod = z.object({
  sourceType: z.enum(["entity", "microflow", "association", "parameter", "static"]),
  entityRef: RefZod.optional(),
  microflowRef: RefZod.optional(),
  associationRef: RefZod.optional(),
  parameterName: z.string().optional()
});

const WidgetBindingSchemaZod = z.object({
  bindingType: z.enum(["value", "items", "entity"]),
  source: z.enum(["attribute", "expression", "parameter", "static"]),
  attributeRef: RefZod.optional(),
  expression: ExpressionSchemaZod.optional(),
  parameterName: z.string().optional(),
  staticValue: z.union([z.string(), z.number(), z.boolean(), z.null()]).optional()
});

export const WidgetSchemaZod: z.ZodTypeAny = z.lazy(() =>
  z.object({
    widgetId: z.string(),
    widgetType: z.enum([
      "container",
      "dataView",
      "textBox",
      "textArea",
      "numberInput",
      "dropDown",
      "button",
      "dataGrid",
      "listView",
      "label"
    ]),
    props: z.record(z.unknown()),
    bindings: z.array(WidgetBindingSchemaZod).optional(),
    children: z.array(WidgetSchemaZod).optional(),
    slots: z.record(z.array(WidgetSchemaZod)).optional(),
    events: z
      .array(
        z.object({
          eventName: z.enum(["onClick", "onChange", "onLoad"]),
          action: z.object({
            actionType: z.enum(["callMicroflow", "callWorkflow", "showMessage"]),
            microflowRef: RefZod.optional(),
            workflowRef: RefZod.optional(),
            message: z.string().optional(),
            arguments: z.array(z.object({ name: z.string(), value: z.unknown() })).optional()
          })
        })
      )
      .optional(),
    visibility: z.object({ expression: ExpressionSchemaZod }).optional(),
    editability: z.object({ expression: ExpressionSchemaZod }).optional(),
    style: z.record(z.unknown()).optional(),
    layout: z.record(z.unknown()).optional(),
    dataSource: DataSourceSchemaZod.optional(),
    fieldBinding: WidgetBindingSchemaZod.optional(),
    action: z
      .object({
        actionType: z.enum(["callMicroflow", "callWorkflow", "showMessage"]),
        microflowRef: RefZod.optional(),
        workflowRef: RefZod.optional(),
        message: z.string().optional(),
        arguments: z.array(z.object({ name: z.string(), value: z.unknown() })).optional()
      })
      .optional()
  })
);

const PageSchemaZod = z.object({
  pageId: z.string(),
  moduleId: z.string(),
  name: z.string(),
  pageType: z.enum(["responsive", "popup", "layout", "snippet"]),
  layoutRef: RefZod.optional(),
  parameters: z.array(z.object({ name: z.string(), type: DataTypeSchemaZod })),
  rootWidget: WidgetSchemaZod,
  allowedRoles: z.array(RefZod)
});

const MicroflowNodeSchemaZod = z.object({
  nodeId: z.string(),
  type: z.enum([
    "startEvent",
    "endEvent",
    "decision",
    "retrieveObject",
    "changeObject",
    "commitObject",
    "createVariable",
    "changeVariable",
    "showMessage",
    "validationFeedback",
    "callWorkflow",
    "callMicroflow"
  ]),
  caption: z.string(),
  position: z.object({ x: z.number(), y: z.number() })
});

const WorkflowNodeSchemaZod = z.object({
  nodeId: z.string(),
  type: z.enum(["startEvent", "endEvent", "decision", "userTask", "callMicroflow", "timer", "annotation"]),
  caption: z.string(),
  position: z.object({ x: z.number(), y: z.number() })
});

const MicroflowSchemaZod = z.object({
  microflowId: z.string(),
  moduleId: z.string(),
  name: z.string(),
  parameters: z.array(z.object({ name: z.string(), type: DataTypeSchemaZod })),
  returnType: DataTypeSchemaZod,
  allowedRoles: z.array(RefZod),
  applyEntityAccess: z.boolean(),
  concurrentExecution: z.object({
    allowConcurrentExecution: z.boolean(),
    onBlocked: z.enum(["showMessage", "callMicroflow", "throwError"]).optional(),
    blockedMessage: z.string().optional(),
    blockedMicroflowRef: RefZod.optional()
  }),
  nodes: z.array(MicroflowNodeSchemaZod),
  edges: z.array(
    z.object({
      edgeId: z.string(),
      fromNodeId: z.string(),
      toNodeId: z.string(),
      outcome: z.string().optional()
    })
  )
});

const WorkflowSchemaZod = z.object({
  workflowId: z.string(),
  moduleId: z.string(),
  name: z.string(),
  contextEntityRef: RefZod,
  parameters: z.array(z.object({ name: z.string(), type: DataTypeSchemaZod })),
  nodes: z.array(WorkflowNodeSchemaZod),
  edges: z.array(
    z.object({
      edgeId: z.string(),
      fromNodeId: z.string(),
      toNodeId: z.string(),
      sequence: z.number().optional(),
      decisionOutcome: z.string().optional(),
      taskOutcome: z.string().optional(),
      timerOutcome: z.string().optional()
    })
  )
});

const SecuritySchemaZod = z.object({
  securityLevel: z.enum(["off", "prototype", "production"]),
  userRoles: z.array(
    z.object({
      roleId: z.string(),
      name: z.string(),
      moduleRoleRefs: z.array(RefZod)
    })
  ),
  moduleRoles: z.array(
    z.object({
      roleId: z.string(),
      moduleId: z.string(),
      name: z.string()
    })
  ),
  pageAccessRules: z.array(z.object({ pageRef: RefZod, roleRefs: z.array(RefZod) })),
  microflowAccessRules: z.array(z.object({ microflowRef: RefZod, roleRefs: z.array(RefZod) })),
  nanoflowAccessRules: z.array(z.object({ nanoflowName: z.string(), roleRefs: z.array(RefZod) })),
  entityAccessRules: z.array(EntityAccessRuleSchemaZod)
});

export const LowCodeAppSchemaZod = z.object({
  appId: z.string(),
  name: z.string(),
  version: z.string(),
  modules: z.array(
    z.object({
      moduleId: z.string(),
      name: z.string(),
      domainModel: z.object({
        entities: z.array(EntitySchemaZod),
        associations: z.array(AssociationSchemaZod),
        enumerations: z.array(EnumerationSchemaZod)
      }),
      pages: z.array(PageSchemaZod),
      microflows: z.array(MicroflowSchemaZod),
      workflows: z.array(WorkflowSchemaZod),
      enumerations: z.array(EnumerationSchemaZod)
    })
  ),
  navigation: z.array(
    z.object({
      itemId: z.string(),
      caption: z.string(),
      pageRef: RefZod
    })
  ),
  security: SecuritySchemaZod,
  extensions: z.array(
    z.object({
      extensionId: z.string(),
      name: z.string(),
      version: z.string(),
      contributes: z
        .object({
          widgetTypes: z.array(z.string()).optional(),
          microflowNodeTypes: z.array(z.string()).optional(),
          workflowNodeTypes: z.array(z.string()).optional()
        })
        .default({})
    })
  )
});

export function parseLowCodeAppSchema(input: unknown) {
  return LowCodeAppSchemaZod.parse(input);
}
