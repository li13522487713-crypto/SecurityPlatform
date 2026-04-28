import { parseExpression } from "@atlas/mendix-expression";
import type { ExpressionSchema, LowCodeAppSchema } from "@atlas/mendix-schema";

function expr(source: string): ExpressionSchema {
  const parsed = parseExpression(source);
  return {
    ...parsed,
    source,
    dependencies: parsed.dependencies
  };
}

export const SAMPLE_PROCUREMENT_APP: LowCodeAppSchema = {
  appId: "app_procurement",
  name: "Procurement Approval",
  version: "1.0.0",
  modules: [
    {
      moduleId: "mod_procurement",
      name: "Procurement",
      domainModel: {
        entities: [
          {
            entityId: "ent_purchase_request",
            moduleId: "mod_procurement",
            name: "PurchaseRequest",
            entityType: "persistable",
            attributes: [
              {
                attributeId: "Amount",
                entityId: "ent_purchase_request",
                name: "Amount",
                attributeType: "Decimal",
                dataType: { kind: "Decimal", precision: 18, scale: 2 }
              },
              {
                attributeId: "Status",
                entityId: "ent_purchase_request",
                name: "Status",
                attributeType: "Enumeration",
                dataType: { kind: "Enumeration", enumerationRef: { kind: "enumeration", id: "enum_purchase_status" } }
              },
              {
                attributeId: "Reason",
                entityId: "ent_purchase_request",
                name: "Reason",
                attributeType: "String",
                dataType: { kind: "String" }
              },
              {
                attributeId: "SubmitTime",
                entityId: "ent_purchase_request",
                name: "SubmitTime",
                attributeType: "DateTime",
                dataType: { kind: "DateTime" }
              }
            ],
            associations: [],
            accessRules: [],
            validationRules: [],
            eventHandlers: [],
            systemMembers: {
              storeOwner: true,
              storeCreatedDate: true,
              storeChangedDate: true
            }
          },
          {
            entityId: "ent_department",
            moduleId: "mod_procurement",
            name: "Department",
            entityType: "persistable",
            attributes: [
              {
                attributeId: "Name",
                entityId: "ent_department",
                name: "Name",
                attributeType: "String",
                dataType: { kind: "String" }
              }
            ],
            associations: [],
            accessRules: [],
            validationRules: [],
            eventHandlers: [],
            systemMembers: {
              storeOwner: true,
              storeCreatedDate: true,
              storeChangedDate: true
            }
          },
          {
            entityId: "ent_account",
            moduleId: "mod_procurement",
            name: "Account",
            entityType: "persistable",
            attributes: [
              {
                attributeId: "UserName",
                entityId: "ent_account",
                name: "UserName",
                attributeType: "String",
                dataType: { kind: "String" }
              }
            ],
            associations: [],
            accessRules: [],
            validationRules: [],
            eventHandlers: [],
            systemMembers: {
              storeOwner: true,
              storeCreatedDate: true,
              storeChangedDate: true
            }
          },
          {
            entityId: "ent_approval_comment",
            moduleId: "mod_procurement",
            name: "ApprovalComment",
            entityType: "persistable",
            attributes: [
              {
                attributeId: "Comment",
                entityId: "ent_approval_comment",
                name: "Comment",
                attributeType: "String",
                dataType: { kind: "String" }
              }
            ],
            associations: [],
            accessRules: [],
            validationRules: [],
            eventHandlers: [],
            systemMembers: {
              storeOwner: true,
              storeCreatedDate: true,
              storeChangedDate: true
            }
          }
        ],
        associations: [],
        enumerations: [
          {
            enumerationId: "enum_purchase_status",
            moduleId: "mod_procurement",
            name: "PurchaseStatus",
            values: [
              { key: "Draft", caption: "Draft" },
              { key: "Submitted", caption: "Submitted" },
              { key: "NeedManagerApproval", caption: "NeedManagerApproval" },
              { key: "NeedFinanceApproval", caption: "NeedFinanceApproval" },
              { key: "Approved", caption: "Approved" },
              { key: "Rejected", caption: "Rejected" }
            ]
          }
        ]
      },
      pages: [
        {
          pageId: "page_purchase_request_edit",
          moduleId: "mod_procurement",
          name: "PurchaseRequest_EditPage",
          pageType: "responsive",
          parameters: [
            {
              name: "Request",
              type: { kind: "Object", entityRef: { kind: "entity", id: "ent_purchase_request" } }
            }
          ],
          allowedRoles: [{ kind: "moduleRole", id: "role_requester" }],
          rootWidget: {
            widgetId: "root_container",
            widgetType: "container",
            props: {},
            children: [
              {
                widgetId: "request_data_view",
                widgetType: "dataView",
                props: { title: "Purchase Request" },
                dataSource: {
                  sourceType: "entity",
                  entityRef: { kind: "entity", id: "ent_purchase_request" }
                },
                children: [
                  {
                    widgetId: "input_amount",
                    widgetType: "numberInput",
                    props: { caption: "Amount" },
                    fieldBinding: {
                      bindingType: "value",
                      source: "attribute",
                      attributeRef: { kind: "attribute", id: "Amount" }
                    }
                  },
                  {
                    widgetId: "input_reason",
                    widgetType: "textArea",
                    props: { caption: "Reason" },
                    fieldBinding: {
                      bindingType: "value",
                      source: "attribute",
                      attributeRef: { kind: "attribute", id: "Reason" }
                    }
                  },
                  {
                    widgetId: "input_status",
                    widgetType: "dropDown",
                    props: {
                      caption: "Status",
                      options: [
                        { label: "Draft", value: "Draft" },
                        { label: "Submitted", value: "Submitted" },
                        { label: "NeedManagerApproval", value: "NeedManagerApproval" },
                        { label: "NeedFinanceApproval", value: "NeedFinanceApproval" },
                        { label: "Approved", value: "Approved" },
                        { label: "Rejected", value: "Rejected" }
                      ]
                    },
                    fieldBinding: {
                      bindingType: "value",
                      source: "attribute",
                      attributeRef: { kind: "attribute", id: "Status" }
                    }
                  },
                  {
                    widgetId: "btn_submit",
                    widgetType: "button",
                    props: { caption: "Submit" },
                    action: {
                      actionType: "callMicroflow",
                      microflowRef: { kind: "microflow", id: "mf_submit_purchase_request" },
                      arguments: [{ name: "Request", value: "$Request" }]
                    }
                  }
                ]
              }
            ]
          }
        }
      ],
      microflows: [
        {
          microflowId: "mf_submit_purchase_request",
          moduleId: "mod_procurement",
          name: "MF_SubmitPurchaseRequest",
          parameters: [
            {
              name: "Request",
              type: { kind: "Object", entityRef: { kind: "entity", id: "ent_purchase_request" } }
            }
          ],
          returnType: { kind: "Object", entityRef: { kind: "entity", id: "ent_purchase_request" } },
          allowedRoles: [{ kind: "moduleRole", id: "role_requester" }],
          applyEntityAccess: true,
          concurrentExecution: { allowConcurrentExecution: true },
          nodes: [
            {
              nodeId: "mf_start",
              type: "startEvent",
              caption: "Start",
              position: { x: 80, y: 120 }
            },
            {
              nodeId: "mf_decision_amount",
              type: "decision",
              caption: "Amount > 50000",
              position: { x: 220, y: 120 },
              expression: expr("$Request/Amount > 50000")
            },
            {
              nodeId: "mf_change_status",
              type: "changeObject",
              caption: "Set Status",
              position: { x: 420, y: 120 },
              objectVariable: "Request",
              memberChanges: [
                {
                  memberName: "Status",
                  valueExpression: expr("if $Request/Amount > 50000 then PurchaseStatus.NeedFinanceApproval else PurchaseStatus.NeedManagerApproval")
                }
              ],
              commit: {
                enabled: true,
                withEvents: true,
                refreshInClient: true
              }
            },
            {
              nodeId: "mf_call_workflow",
              type: "callWorkflow",
              caption: "Call WF_PurchaseApproval",
              position: { x: 620, y: 120 },
              workflowRef: { kind: "workflow", id: "wf_purchase_approval" },
              arguments: [expr("$Request")]
            },
            {
              nodeId: "mf_end",
              type: "endEvent",
              caption: "End",
              position: { x: 820, y: 120 },
              returnExpression: expr("$Request")
            }
          ],
          edges: [
            { edgeId: "mf_e1", fromNodeId: "mf_start", toNodeId: "mf_decision_amount" },
            { edgeId: "mf_e2", fromNodeId: "mf_decision_amount", toNodeId: "mf_change_status", outcome: "true" },
            { edgeId: "mf_e3", fromNodeId: "mf_decision_amount", toNodeId: "mf_change_status", outcome: "false" },
            { edgeId: "mf_e4", fromNodeId: "mf_change_status", toNodeId: "mf_call_workflow" },
            { edgeId: "mf_e5", fromNodeId: "mf_call_workflow", toNodeId: "mf_end" }
          ]
        }
      ],
      workflows: [
        {
          workflowId: "wf_purchase_approval",
          moduleId: "mod_procurement",
          name: "WF_PurchaseApproval",
          contextEntityRef: { kind: "entity", id: "ent_purchase_request" },
          parameters: [{ name: "Request", type: { kind: "Object", entityRef: { kind: "entity", id: "ent_purchase_request" } } }],
          nodes: [
            { nodeId: "wf_start", type: "startEvent", caption: "Start", position: { x: 80, y: 100 } },
            {
              nodeId: "wf_decision_amount",
              type: "decision",
              caption: "Amount > 50000",
              position: { x: 250, y: 100 },
              expression: expr("$Request/Amount > 50000"),
              outcomes: ["true", "false"]
            },
            {
              nodeId: "wf_finance_task",
              type: "userTask",
              caption: "Finance User Task",
              position: { x: 460, y: 20 },
              taskName: "FinanceApproval",
              taskDescription: "Finance approval for high amount request",
              taskPageRef: { kind: "page", id: "page_purchase_request_edit" },
              targetUsers: [{ kind: "userRole", id: "role_finance" }],
              outcomes: [
                { key: "Approve", caption: "Approve" },
                { key: "Reject", caption: "Reject" }
              ]
            },
            {
              nodeId: "wf_manager_task",
              type: "userTask",
              caption: "Manager User Task",
              position: { x: 460, y: 180 },
              taskName: "ManagerApproval",
              taskDescription: "Manager approval for normal request",
              taskPageRef: { kind: "page", id: "page_purchase_request_edit" },
              targetUsers: [{ kind: "userRole", id: "role_manager" }],
              outcomes: [
                { key: "Approve", caption: "Approve" },
                { key: "Reject", caption: "Reject" }
              ]
            },
            { nodeId: "wf_end_approved", type: "endEvent", caption: "End Approved", position: { x: 760, y: 40 } },
            { nodeId: "wf_end_rejected", type: "endEvent", caption: "End Rejected", position: { x: 760, y: 220 } }
          ],
          edges: [
            { edgeId: "wf_e1", fromNodeId: "wf_start", toNodeId: "wf_decision_amount" },
            { edgeId: "wf_e2", fromNodeId: "wf_decision_amount", toNodeId: "wf_finance_task", decisionOutcome: "true", sequence: 1 },
            { edgeId: "wf_e3", fromNodeId: "wf_decision_amount", toNodeId: "wf_manager_task", decisionOutcome: "false", sequence: 2 },
            { edgeId: "wf_e4", fromNodeId: "wf_finance_task", toNodeId: "wf_end_approved", taskOutcome: "Approve", sequence: 3 },
            { edgeId: "wf_e5", fromNodeId: "wf_finance_task", toNodeId: "wf_end_rejected", taskOutcome: "Reject", sequence: 4 },
            { edgeId: "wf_e6", fromNodeId: "wf_manager_task", toNodeId: "wf_end_approved", taskOutcome: "Approve", sequence: 5 },
            { edgeId: "wf_e7", fromNodeId: "wf_manager_task", toNodeId: "wf_end_rejected", taskOutcome: "Reject", sequence: 6 }
          ]
        }
      ],
      enumerations: []
    },
    {
      moduleId: "mod_administration",
      name: "Administration",
      domainModel: {
        entities: [],
        associations: [],
        enumerations: []
      },
      pages: [],
      microflows: [],
      workflows: [],
      enumerations: []
    }
  ],
  navigation: [
    {
      itemId: "nav_purchase_request",
      caption: "Purchase Request",
      pageRef: { kind: "page", id: "page_purchase_request_edit" }
    }
  ],
  security: {
    securityLevel: "production",
    userRoles: [
      { roleId: "user_role_requester", name: "Requester", moduleRoleRefs: [{ kind: "moduleRole", id: "role_requester" }] },
      { roleId: "user_role_manager", name: "Manager", moduleRoleRefs: [{ kind: "moduleRole", id: "role_manager" }] },
      { roleId: "user_role_finance", name: "Finance", moduleRoleRefs: [{ kind: "moduleRole", id: "role_finance" }] },
      { roleId: "user_role_admin", name: "Admin", moduleRoleRefs: [{ kind: "moduleRole", id: "role_admin" }] }
    ],
    moduleRoles: [
      { roleId: "role_requester", moduleId: "mod_procurement", name: "Requester" },
      { roleId: "role_manager", moduleId: "mod_procurement", name: "Manager" },
      { roleId: "role_finance", moduleId: "mod_procurement", name: "Finance" },
      { roleId: "role_admin", moduleId: "mod_administration", name: "Admin" }
    ],
    pageAccessRules: [
      {
        pageRef: { kind: "page", id: "page_purchase_request_edit" },
        roleRefs: [{ kind: "moduleRole", id: "role_requester" }, { kind: "moduleRole", id: "role_manager" }, { kind: "moduleRole", id: "role_finance" }]
      }
    ],
    microflowAccessRules: [
      {
        microflowRef: { kind: "microflow", id: "mf_submit_purchase_request" },
        roleRefs: [{ kind: "moduleRole", id: "role_requester" }]
      }
    ],
    nanoflowAccessRules: [],
    entityAccessRules: [
      {
        ruleId: "entity_access_purchase_request",
        roleRefs: [{ kind: "moduleRole", id: "role_requester" }, { kind: "moduleRole", id: "role_manager" }, { kind: "moduleRole", id: "role_finance" }],
        xpathConstraint: "[%CurrentUser% = Owner]",
        memberAccess: [
          { attributeRef: { kind: "attribute", id: "Amount" }, read: true, write: true },
          { attributeRef: { kind: "attribute", id: "Status" }, read: true, write: true },
          { attributeRef: { kind: "attribute", id: "Reason" }, read: true, write: true },
          { attributeRef: { kind: "attribute", id: "SubmitTime" }, read: true, write: false }
        ]
      }
    ]
  },
  extensions: []
};

export const SAMPLE_RUNTIME_OBJECT: Record<string, unknown> = {
  Amount: 12000,
  Status: "Draft",
  Reason: "采购办公电脑",
  SubmitTime: null
};
