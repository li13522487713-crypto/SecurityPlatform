import { inferExpressionType } from "@atlas/mendix-expression";
import type {
  LowCodeAppSchema,
  ValidationErrorSchema
} from "@atlas/mendix-schema";

type ValidationContext = {
  app: LowCodeAppSchema;
  errors: ValidationErrorSchema[];
};

function pushError(ctx: ValidationContext, error: ValidationErrorSchema) {
  ctx.errors.push(error);
}

function findEntity(ctx: ValidationContext, entityId: string) {
  return ctx.app.modules.flatMap(m => m.domainModel.entities).find(entity => entity.entityId === entityId);
}

function findAttribute(ctx: ValidationContext, attributeId: string) {
  return ctx.app.modules
    .flatMap(m => m.domainModel.entities)
    .flatMap(entity => entity.attributes)
    .find(attribute => attribute.attributeId === attributeId || attribute.name === attributeId);
}

function findMicroflow(ctx: ValidationContext, microflowId: string) {
  return ctx.app.modules.flatMap(m => m.microflows).find(mf => mf.microflowId === microflowId);
}

function validatePageBindings(ctx: ValidationContext) {
  for (const module of ctx.app.modules) {
    for (const page of module.pages) {
      if (page.allowedRoles.length === 0) {
        pushError(ctx, {
          severity: "error",
          code: "PAGE_ACCESS_ROLE_REQUIRED",
          message: `页面 ${page.name} 未配置访问角色`,
          target: { kind: "page", id: page.pageId, path: "allowedRoles" }
        });
      }

      const stack = [page.rootWidget];
      while (stack.length > 0) {
        const widget = stack.pop();
        if (!widget) {
          continue;
        }

        if (widget.widgetType === "dataView" && widget.dataSource?.entityRef) {
          const entity = findEntity(ctx, widget.dataSource.entityRef.id);
          if (!entity) {
            pushError(ctx, {
              severity: "error",
              code: "PAGE_ENTITY_NOT_FOUND",
              message: `页面 ${page.name} 引用了不存在实体 ${widget.dataSource.entityRef.id}`,
              target: { kind: "widget", id: widget.widgetId, path: "dataSource.entityRef" }
            });
          }
        }

        const fieldBinding = (widget as { fieldBinding?: { attributeRef?: { id: string } } }).fieldBinding;
        if (fieldBinding?.attributeRef && !findAttribute(ctx, fieldBinding.attributeRef.id)) {
          pushError(ctx, {
            severity: "error",
            code: "WIDGET_ATTRIBUTE_NOT_FOUND",
            message: `组件 ${widget.widgetId} 绑定了不存在属性 ${fieldBinding.attributeRef.id}`,
            target: { kind: "widget", id: widget.widgetId, path: "fieldBinding.attributeRef" }
          });
        }

        widget.children?.forEach(child => stack.push(child));
        Object.values(widget.slots ?? {}).forEach(slot => slot.forEach(child => stack.push(child)));
      }
    }
  }
}

function validateMicroflows(ctx: ValidationContext) {
  for (const module of ctx.app.modules) {
    for (const microflow of module.microflows) {
      const scope = {
        variables: Object.fromEntries(microflow.parameters.map(param => [`$${param.name}`, param.type])),
        enumerations: {}
      };
      const variableNames = new Set(microflow.parameters.map(p => p.name));

      for (const node of microflow.nodes) {
        if (node.type === "decision") {
          const decisionType = inferExpressionType(node.expression.ast, scope);
          if (decisionType.kind !== "Boolean" && decisionType.kind !== "Enumeration") {
            pushError(ctx, {
              severity: "error",
              code: "DECISION_TYPE_INVALID",
              message: `Decision 节点 ${node.nodeId} 表达式必须返回 Boolean 或 Enumeration`,
              target: { kind: "microflowNode", id: node.nodeId, path: "expression" }
            });
          }
        }

        if (node.type === "endEvent" && node.returnExpression) {
          const returnType = inferExpressionType(node.returnExpression.ast, scope);
          if (returnType.kind !== microflow.returnType.kind) {
            pushError(ctx, {
              severity: "error",
              code: "MICROFLOW_RETURN_TYPE_MISMATCH",
              message: `Microflow ${microflow.name} End Event 返回类型不匹配`,
              target: { kind: "microflowNode", id: node.nodeId, path: "returnExpression" }
            });
          }
        }

        if (node.type === "callMicroflow") {
          const called = findMicroflow(ctx, node.microflowRef.id);
          if (!called) {
            pushError(ctx, {
              severity: "error",
              code: "CALL_MICROFLOW_NOT_FOUND",
              message: `调用了不存在的 Microflow ${node.microflowRef.id}`,
              target: { kind: "microflowNode", id: node.nodeId, path: "microflowRef" }
            });
          } else {
            if (node.arguments.length !== called.parameters.length) {
              pushError(ctx, {
                severity: "error",
                code: "CALL_MICROFLOW_ARG_COUNT_MISMATCH",
                message: `Microflow 调用参数数量不匹配`,
                target: { kind: "microflowNode", id: node.nodeId, path: "arguments" }
              });
            }
            node.arguments.forEach((arg, index) => {
              const inferred = inferExpressionType(arg.ast, scope);
              const expected = called.parameters[index]?.type;
              if (expected && inferred.kind !== expected.kind) {
                pushError(ctx, {
                  severity: "error",
                  code: "CALL_MICROFLOW_ARG_TYPE_MISMATCH",
                  message: `Microflow 调用参数类型不匹配`,
                  target: { kind: "microflowNode", id: node.nodeId, path: `arguments.${index}` }
                });
              }
            });
          }
        }

        if (node.type === "retrieveObject" && !node.entityRef) {
          pushError(ctx, {
            severity: "error",
            code: "RETRIEVE_OBJECT_ENTITY_REQUIRED",
            message: "Retrieve Object 缺少实体引用",
            target: { kind: "microflowNode", id: node.nodeId, path: "entityRef" }
          });
        }

        if (node.type === "changeObject") {
          node.memberChanges.forEach(change => {
            if (!findAttribute(ctx, change.memberName)) {
              pushError(ctx, {
                severity: "error",
                code: "CHANGE_OBJECT_MEMBER_NOT_FOUND",
                message: `Change Object 修改了不存在字段 ${change.memberName}`,
                target: { kind: "microflowNode", id: node.nodeId, path: "memberChanges" }
              });
            }
          });
        }

        if (node.type === "commitObject" && !variableNames.has(node.targetVariableName)) {
          pushError(ctx, {
            severity: "error",
            code: "COMMIT_OBJECT_VARIABLE_NOT_FOUND",
            message: `Commit Object 目标变量不存在: ${node.targetVariableName}`,
            target: { kind: "microflowNode", id: node.nodeId, path: "targetVariableName" }
          });
        }

        if (node.type === "createVariable" || node.type === "changeVariable") {
          variableNames.add(node.variableName);
        }
      }
    }
  }
}

function validateWorkflows(ctx: ValidationContext) {
  for (const module of ctx.app.modules) {
    for (const workflow of module.workflows) {
      for (const node of workflow.nodes) {
        if (node.type === "userTask" && node.outcomes.length === 0) {
          pushError(ctx, {
            severity: "error",
            code: "WORKFLOW_USER_TASK_OUTCOME_REQUIRED",
            message: "Workflow User Task 未配置 Outcome",
            target: { kind: "workflowNode", id: node.nodeId, path: "outcomes" }
          });
        }
      }

      const outgoingByNode = new Map<string, number>();
      workflow.edges.forEach(edge => {
        outgoingByNode.set(edge.fromNodeId, (outgoingByNode.get(edge.fromNodeId) ?? 0) + 1);
      });

      workflow.nodes.forEach(node => {
        if (node.type === "decision" && (outgoingByNode.get(node.nodeId) ?? 0) < 2) {
          pushError(ctx, {
            severity: "error",
            code: "WORKFLOW_DECISION_INCOMPLETE_EDGES",
            message: "Workflow Decision 出边不完整",
            target: { kind: "workflowNode", id: node.nodeId, path: "edges" }
          });
        }
      });
    }
  }
}

function validateSecurity(ctx: ValidationContext) {
  for (const rule of ctx.app.security.entityAccessRules) {
    if (rule.memberAccess.length === 0) {
      pushError(ctx, {
        severity: "error",
        code: "ENTITY_ACCESS_MEMBER_REQUIRED",
        message: "Entity Access 缺少字段权限",
        target: { kind: "security", id: rule.ruleId, path: "memberAccess" }
      });
    }
  }
}

function validateNavigation(ctx: ValidationContext) {
  const pages = new Set(ctx.app.modules.flatMap(module => module.pages.map(page => page.pageId)));
  ctx.app.navigation.forEach(item => {
    if (!pages.has(item.pageRef.id)) {
      pushError(ctx, {
        severity: "error",
        code: "NAVIGATION_PAGE_NOT_FOUND",
        message: `导航引用不存在页面 ${item.pageRef.id}`,
        target: { kind: "navigation", id: item.itemId, path: "pageRef" }
      });
    }
  });
}

function validateEnumerationUsage(ctx: ValidationContext) {
  const existingEnumValues = new Set(
    ctx.app.modules
      .flatMap(module => module.domainModel.enumerations)
      .flatMap(enumeration => enumeration.values.map(value => `${enumeration.name}.${value.key}`))
  );
  for (const module of ctx.app.modules) {
    for (const microflow of module.microflows) {
      for (const node of microflow.nodes) {
        if (node.type === "decision") {
          const source = node.expression.source;
          const match = /([A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*)/.exec(source);
          if (match && !existingEnumValues.has(match[1])) {
            pushError(ctx, {
              severity: "warning",
              code: "ENUM_VALUE_REMOVED",
              message: `表达式引用了不存在枚举值 ${match[1]}`,
              target: { kind: "microflowNode", id: node.nodeId, path: "expression" }
            });
          }
        }
      }
    }
  }
}

export function validateLowCodeAppSchema(app: LowCodeAppSchema): ValidationErrorSchema[] {
  const ctx: ValidationContext = { app, errors: [] };
  validatePageBindings(ctx);
  validateMicroflows(ctx);
  validateWorkflows(ctx);
  validateSecurity(ctx);
  validateEnumerationUsage(ctx);
  validateNavigation(ctx);
  return ctx.errors;
}
