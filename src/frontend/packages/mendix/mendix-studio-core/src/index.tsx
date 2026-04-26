import { useMemo, useState } from "react";
import {
  Badge,
  Button,
  Card,
  Collapse,
  Divider,
  Empty,
  Input,
  InputNumber,
  Select,
  SideSheet,
  Space,
  Table,
  Tag,
  TextArea,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import { IconArrowRight, IconPlus, IconSave } from "@douyinfe/semi-icons";
import { createLocalMicroflowApiClient, MicroflowEditor, sampleMicroflowSchema } from "@atlas/microflow";
import { DebugTracePanel } from "@atlas/mendix-debug";
import { createRuntimeExecutor, RuntimeRenderer } from "@atlas/mendix-runtime";
import type { LowCodeAppSchema, WidgetSchema, WidgetType } from "@atlas/mendix-schema";
import { validateLowCodeAppSchema } from "@atlas/mendix-validator";
import { useMendixStudioStore, type MendixStudioTab } from "./store";

const { Text, Title } = Typography;

const WIDGET_TOOLBOX: WidgetType[] = [
  "container",
  "dataView",
  "textBox",
  "textArea",
  "numberInput",
  "dropDown",
  "button",
  "label"
];

function updateSchema(mutator: (schema: LowCodeAppSchema) => void) {
  const current = useMendixStudioStore.getState().appSchema;
  const next = JSON.parse(JSON.stringify(current)) as LowCodeAppSchema;
  mutator(next);
  useMendixStudioStore.getState().setAppSchema(next);
}

function AppExplorer() {
  const app = useMendixStudioStore(state => state.appSchema);
  const setActiveTab = useMendixStudioStore(state => state.setActiveTab);
  const setSelected = useMendixStudioStore(state => state.setSelected);

  const firstModule = app.modules[0];
  const firstPage = firstModule.pages[0];
  const firstMicroflow = firstModule.microflows[0];
  const firstWorkflow = firstModule.workflows[0];

  return (
    <Space vertical style={{ width: "100%" }}>
      <Button block onClick={() => setActiveTab("domainModel")}>Domain Model</Button>
      <Button
        block
        onClick={() => {
          setSelected("page", firstPage?.pageId ?? "");
          setActiveTab("pageBuilder");
        }}
      >
        Pages
      </Button>
      <Button
        block
        onClick={() => {
          setSelected("microflow", firstMicroflow?.microflowId ?? "");
          setActiveTab("microflowDesigner");
        }}
      >
        Microflows
      </Button>
      <Button
        block
        onClick={() => {
          setSelected("workflow", firstWorkflow?.workflowId ?? "");
          setActiveTab("workflowDesigner");
        }}
      >
        Workflows
      </Button>
      <Button block onClick={() => setActiveTab("securityEditor")}>Security</Button>
      <Button block onClick={() => setActiveTab("runtimePreview")}>Runtime Preview</Button>
      <Divider />
      <Text type="tertiary">Modules</Text>
      {app.modules.map(module => (
        <Badge key={module.moduleId} count={module.pages.length + module.microflows.length + module.workflows.length}>
          <Tag>{module.name}</Tag>
        </Badge>
      ))}
    </Space>
  );
}

function DomainModelDesigner() {
  const app = useMendixStudioStore(state => state.appSchema);
  const module = app.modules[0];
  const [newEntityName, setNewEntityName] = useState("");
  const [newEnumName, setNewEnumName] = useState("");

  return (
    <Space vertical style={{ width: "100%" }}>
      <Card title="Entities">
        <Space style={{ marginBottom: 8 }}>
          <Input value={newEntityName} onChange={setNewEntityName} placeholder="New Entity Name" />
          <Button
            icon={<IconPlus />}
            onClick={() => {
              const name = newEntityName.trim();
              if (!name) {
                Toast.warning("请输入实体名称");
                return;
              }
              updateSchema(schema => {
                schema.modules[0].domainModel.entities.push({
                  entityId: `ent_${Date.now()}`,
                  moduleId: schema.modules[0].moduleId,
                  name,
                  entityType: "persistable",
                  attributes: [],
                  associations: [],
                  accessRules: [],
                  validationRules: [],
                  eventHandlers: [],
                  systemMembers: { storeOwner: true, storeCreatedDate: true, storeChangedDate: true }
                });
              });
              setNewEntityName("");
            }}
          >
            Add Entity
          </Button>
        </Space>
        <Collapse>
          {module.domainModel.entities.map(entity => (
            <Collapse.Panel key={entity.entityId} itemKey={entity.entityId} header={entity.name}>
              <Space vertical style={{ width: "100%" }}>
                <Button
                  size="small"
                  onClick={() =>
                    updateSchema(schema => {
                      const target = schema.modules[0].domainModel.entities.find(item => item.entityId === entity.entityId);
                      if (!target) {
                        return;
                      }
                      target.attributes.push({
                        attributeId: `attr_${Date.now()}`,
                        entityId: target.entityId,
                        name: `Field${target.attributes.length + 1}`,
                        attributeType: "String",
                        dataType: { kind: "String" }
                      });
                    })
                  }
                >
                  Add Attribute
                </Button>
                {entity.attributes.map(attribute => (
                  <Space key={attribute.attributeId}>
                    <Text>{attribute.name}</Text>
                    <Select
                      value={attribute.attributeType}
                      onChange={value =>
                        updateSchema(schema => {
                          const targetEntity = schema.modules[0].domainModel.entities.find(item => item.entityId === entity.entityId);
                          const targetAttribute = targetEntity?.attributes.find(item => item.attributeId === attribute.attributeId);
                          if (!targetAttribute) {
                            return;
                          }
                          targetAttribute.attributeType = value as typeof targetAttribute.attributeType;
                          targetAttribute.dataType =
                            value === "Decimal"
                              ? { kind: "Decimal", precision: 18, scale: 2 }
                              : value === "DateTime"
                                ? { kind: "DateTime" }
                                : value === "Boolean"
                                  ? { kind: "Boolean" }
                                  : value === "Integer"
                                    ? { kind: "Integer" }
                                    : value === "Long"
                                      ? { kind: "Long" }
                                      : value === "Enumeration"
                                        ? { kind: "Enumeration", enumerationRef: { kind: "enumeration", id: "enum_purchase_status" } }
                                        : value === "Binary"
                                          ? { kind: "Binary" }
                                          : { kind: "String" };
                        })
                      }
                      optionList={[
                        { value: "String", label: "String" },
                        { value: "Boolean", label: "Boolean" },
                        { value: "Integer", label: "Integer" },
                        { value: "Long", label: "Long" },
                        { value: "Decimal", label: "Decimal" },
                        { value: "DateTime", label: "DateTime" },
                        { value: "Enumeration", label: "Enumeration" },
                        { value: "Binary", label: "Binary" },
                        { value: "AutoNumber", label: "AutoNumber" }
                      ]}
                    />
                    <Button
                      type="danger"
                      theme="borderless"
                      onClick={() =>
                        updateSchema(schema => {
                          const targetEntity = schema.modules[0].domainModel.entities.find(item => item.entityId === entity.entityId);
                          if (!targetEntity) {
                            return;
                          }
                          const referenced = schema.modules[0].pages.some(page =>
                            JSON.stringify(page.rootWidget).includes(attribute.attributeId)
                          );
                          if (referenced) {
                            Toast.warning("该属性已被页面引用，不能删除");
                            return;
                          }
                          targetEntity.attributes = targetEntity.attributes.filter(item => item.attributeId !== attribute.attributeId);
                        })
                      }
                    >
                      Delete
                    </Button>
                  </Space>
                ))}
              </Space>
            </Collapse.Panel>
          ))}
        </Collapse>
      </Card>

      <Card title="Associations">
        <Button
          icon={<IconPlus />}
          onClick={() =>
            updateSchema(schema => {
              const entities = schema.modules[0].domainModel.entities;
              if (entities.length < 2) {
                Toast.warning("至少需要两个实体");
                return;
              }
              schema.modules[0].domainModel.associations.push({
                associationId: `assoc_${Date.now()}`,
                moduleId: schema.modules[0].moduleId,
                name: `${entities[0].name}_${entities[1].name}`,
                fromEntityRef: { kind: "entity", id: entities[0].entityId },
                toEntityRef: { kind: "entity", id: entities[1].entityId },
                owner: "default",
                cardinality: "oneToMany"
              });
            })
          }
        >
          Add Association
        </Button>
        {module.domainModel.associations.map(association => (
          <Tag key={association.associationId}>{association.name}</Tag>
        ))}
      </Card>

      <Card title="Enumerations">
        <Space>
          <Input value={newEnumName} onChange={setNewEnumName} placeholder="New Enumeration Name" />
          <Button
            onClick={() => {
              const name = newEnumName.trim();
              if (!name) {
                return;
              }
              updateSchema(schema => {
                schema.modules[0].domainModel.enumerations.push({
                  enumerationId: `enum_${Date.now()}`,
                  moduleId: schema.modules[0].moduleId,
                  name,
                  values: [{ key: "Value1", caption: "Value1" }]
                });
              });
              setNewEnumName("");
            }}
          >
            Add Enumeration
          </Button>
        </Space>
        {module.domainModel.enumerations.map(enumeration => (
          <Card key={enumeration.enumerationId} size="small" style={{ marginTop: 8 }}>
            <Text strong>{enumeration.name}</Text>
            <Space vertical>
              {enumeration.values.map(value => (
                <Input
                  key={value.key}
                  value={value.caption}
                  onChange={next =>
                    updateSchema(schema => {
                      const target = schema.modules[0].domainModel.enumerations.find(item => item.enumerationId === enumeration.enumerationId);
                      const targetValue = target?.values.find(item => item.key === value.key);
                      if (targetValue) {
                        targetValue.caption = next;
                      }
                    })
                  }
                />
              ))}
            </Space>
          </Card>
        ))}
      </Card>
    </Space>
  );
}

function walkWidgets(widget: WidgetSchema, output: WidgetSchema[]) {
  output.push(widget);
  widget.children?.forEach(child => walkWidgets(child, output));
  Object.values(widget.slots ?? {}).forEach(slot => slot.forEach(child => walkWidgets(child, output)));
}

function PageBuilder() {
  const app = useMendixStudioStore(state => state.appSchema);
  const [selectedWidgetId, setSelectedWidgetId] = useState<string>("");
  const page = app.modules[0].pages[0];
  const widgets: WidgetSchema[] = [];
  walkWidgets(page.rootWidget, widgets);
  const selectedWidget = widgets.find(widget => widget.widgetId === selectedWidgetId) ?? page.rootWidget;

  return (
    <div style={{ display: "grid", gridTemplateColumns: "220px 1fr 300px", gap: 12 }}>
      <Card title="Toolbox">
        <Space vertical style={{ width: "100%" }}>
          {WIDGET_TOOLBOX.map(type => (
            <Button
              key={type}
              block
              onClick={() =>
                updateSchema(schema => {
                  const root = schema.modules[0].pages[0].rootWidget;
                  root.children = root.children ?? [];
                  root.children.push({
                    widgetId: `widget_${Date.now()}`,
                    widgetType: type,
                    props: { caption: type },
                    children: type === "container" || type === "dataView" ? [] : undefined
                  } as WidgetSchema);
                })
              }
            >
              Add {type}
            </Button>
          ))}
        </Space>
      </Card>
      <Card title="Component Tree">
        <Space vertical>
          {widgets.map(widget => (
            <Button key={widget.widgetId} theme={widget.widgetId === selectedWidget.widgetId ? "solid" : "borderless"} onClick={() => setSelectedWidgetId(widget.widgetId)}>
              {widget.widgetType} / {widget.widgetId}
            </Button>
          ))}
        </Space>
      </Card>
      <Card title="Properties">
        <Space vertical style={{ width: "100%" }}>
          <Input
            value={String(selectedWidget.props.caption ?? selectedWidget.props.text ?? "")}
            onChange={value =>
              updateSchema(schema => {
                const all: WidgetSchema[] = [];
                walkWidgets(schema.modules[0].pages[0].rootWidget, all);
                const target = all.find(item => item.widgetId === selectedWidget.widgetId);
                if (!target) {
                  return;
                }
                target.props.caption = value;
                if (target.widgetType === "label") {
                  target.props.text = value;
                }
              })
            }
            placeholder="caption"
          />
          {(selectedWidget.widgetType === "textBox" || selectedWidget.widgetType === "textArea" || selectedWidget.widgetType === "numberInput" || selectedWidget.widgetType === "dropDown") && (
            <Select
              value={(selectedWidget as { fieldBinding?: { attributeRef?: { id: string } } }).fieldBinding?.attributeRef?.id ?? ""}
              onChange={value =>
                updateSchema(schema => {
                  const all: WidgetSchema[] = [];
                  walkWidgets(schema.modules[0].pages[0].rootWidget, all);
                  const target = all.find(item => item.widgetId === selectedWidget.widgetId);
                  if (!target || !("fieldBinding" in target)) {
                    return;
                  }
                  target.fieldBinding = {
                    bindingType: "value",
                    source: "attribute",
                    attributeRef: { kind: "attribute", id: String(value) }
                  };
                })
              }
              optionList={app.modules[0].domainModel.entities[0].attributes.map(attribute => ({
                label: attribute.name,
                value: attribute.attributeId
              }))}
              placeholder="Bind attribute"
            />
          )}
          {selectedWidget.widgetType === "button" && (
            <Select
              value={(selectedWidget as { action?: { microflowRef?: { id: string } } }).action?.microflowRef?.id ?? ""}
              onChange={value =>
                updateSchema(schema => {
                  const all: WidgetSchema[] = [];
                  walkWidgets(schema.modules[0].pages[0].rootWidget, all);
                  const target = all.find(item => item.widgetId === selectedWidget.widgetId);
                  if (!target || target.widgetType !== "button") {
                    return;
                  }
                  target.action = {
                    actionType: "callMicroflow",
                    microflowRef: { kind: "microflow", id: String(value) },
                    arguments: [{ name: "Request", value: "$Request" }]
                  };
                })
              }
              optionList={app.modules[0].microflows.map(microflow => ({
                label: microflow.name,
                value: microflow.microflowId
              }))}
              placeholder="Bind microflow"
            />
          )}
          <Text type="tertiary">Visibility Expression</Text>
          <TextArea
            rows={3}
            value={selectedWidget.visibility?.expression.source ?? ""}
            onChange={value =>
              updateSchema(schema => {
                const all: WidgetSchema[] = [];
                walkWidgets(schema.modules[0].pages[0].rootWidget, all);
                const target = all.find(item => item.widgetId === selectedWidget.widgetId);
                if (!target) {
                  return;
                }
                target.visibility = value.trim().length > 0
                  ? {
                      expression: {
                        source: value,
                        ast: { type: "literal", value: true },
                        dependencies: [],
                        validation: []
                      }
                    }
                  : undefined;
              })
            }
          />
        </Space>
      </Card>
    </div>
  );
}

function MicroflowDesigner() {
  const app = useMendixStudioStore(state => state.appSchema);
  const [useAdvancedEditor, setUseAdvancedEditor] = useState(false);
  const microflow = app.modules[0].microflows[0];
  const apiClient = useMemo(() => createLocalMicroflowApiClient([sampleMicroflowSchema]), []);

  if (useAdvancedEditor) {
    return (
      <Space vertical style={{ width: "100%" }}>
        <Button onClick={() => setUseAdvancedEditor(false)}>返回 MVP 列表编辑器</Button>
        <div style={{ height: 680 }}>
          <MicroflowEditor schema={sampleMicroflowSchema} apiClient={apiClient} />
        </div>
      </Space>
    );
  }

  return (
    <Space vertical style={{ width: "100%" }}>
      <Space>
        <Button onClick={() => setUseAdvancedEditor(true)}>打开高级微流编辑器 (@atlas/microflow)</Button>
        <Button
          icon={<IconPlus />}
          onClick={() =>
            updateSchema(schema => {
              schema.modules[0].microflows[0].nodes.push({
                nodeId: `mf_node_${Date.now()}`,
                type: "showMessage",
                caption: "Show Message",
                position: { x: 320, y: 280 },
                message: "Hello Mendix"
              });
            })
          }
        >
          Add Node
        </Button>
      </Space>
      <Table
        size="small"
        pagination={false}
        dataSource={microflow.nodes}
        rowKey="nodeId"
        columns={[
          { title: "nodeId", dataIndex: "nodeId" },
          {
            title: "type",
            render: (_, row) => (
              <Select
                value={row.type}
                optionList={[
                  { value: "startEvent", label: "startEvent" },
                  { value: "endEvent", label: "endEvent" },
                  { value: "decision", label: "decision" },
                  { value: "retrieveObject", label: "retrieveObject" },
                  { value: "changeObject", label: "changeObject" },
                  { value: "commitObject", label: "commitObject" },
                  { value: "createVariable", label: "createVariable" },
                  { value: "changeVariable", label: "changeVariable" },
                  { value: "showMessage", label: "showMessage" },
                  { value: "validationFeedback", label: "validationFeedback" },
                  { value: "callWorkflow", label: "callWorkflow" },
                  { value: "callMicroflow", label: "callMicroflow" }
                ]}
                onChange={value =>
                  updateSchema(schema => {
                    const node = schema.modules[0].microflows[0].nodes.find(item => item.nodeId === row.nodeId);
                    if (node) {
                      node.type = value as typeof node.type;
                    }
                  })
                }
              />
            )
          },
          { title: "caption", dataIndex: "caption" },
          {
            title: "position",
            render: (_, row) => `${row.position.x},${row.position.y}`
          }
        ]}
      />
      <Card title="Edges">
        <Button
          icon={<IconPlus />}
          onClick={() =>
            updateSchema(schema => {
              const nodes = schema.modules[0].microflows[0].nodes;
              if (nodes.length >= 2) {
                schema.modules[0].microflows[0].edges.push({
                  edgeId: `edge_${Date.now()}`,
                  fromNodeId: nodes[nodes.length - 2].nodeId,
                  toNodeId: nodes[nodes.length - 1].nodeId
                });
              }
            })
          }
        >
          Add Edge
        </Button>
        {microflow.edges.map(edge => (
          <Tag key={edge.edgeId}>{edge.fromNodeId} → {edge.toNodeId} ({edge.outcome ?? "sequence"})</Tag>
        ))}
      </Card>
    </Space>
  );
}

function WorkflowDesigner() {
  const app = useMendixStudioStore(state => state.appSchema);
  const workflow = app.modules[0].workflows[0];
  return (
    <Space vertical style={{ width: "100%" }}>
      <Card title="Workflow Nodes">
        <Button
          icon={<IconPlus />}
          onClick={() =>
            updateSchema(schema => {
              schema.modules[0].workflows[0].nodes.push({
                nodeId: `wf_node_${Date.now()}`,
                type: "annotation",
                caption: "Annotation",
                position: { x: 400, y: 320 }
              });
            })
          }
        >
          Add Node
        </Button>
        <Table
          size="small"
          pagination={false}
          dataSource={workflow.nodes}
          rowKey="nodeId"
          columns={[
            { title: "nodeId", dataIndex: "nodeId" },
            { title: "type", dataIndex: "type" },
            { title: "caption", dataIndex: "caption" },
            {
              title: "outcomes",
              render: (_, row) =>
                row.type === "userTask"
                  ? row.outcomes.map(outcome => outcome.key).join(", ")
                  : row.type === "decision"
                    ? (row.outcomes ?? []).join(", ")
                    : "-"
            }
          ]}
        />
      </Card>
      <Card title="Workflow Edges">
        <Button
          icon={<IconPlus />}
          onClick={() =>
            updateSchema(schema => {
              const nodes = schema.modules[0].workflows[0].nodes;
              if (nodes.length < 2) {
                return;
              }
              schema.modules[0].workflows[0].edges.push({
                edgeId: `wf_edge_${Date.now()}`,
                fromNodeId: nodes[nodes.length - 2].nodeId,
                toNodeId: nodes[nodes.length - 1].nodeId,
                sequence: schema.modules[0].workflows[0].edges.length + 1
              });
            })
          }
        >
          Add Edge
        </Button>
        {workflow.edges.map(edge => (
          <Tag key={edge.edgeId}>
            {edge.fromNodeId} → {edge.toNodeId}
            {edge.decisionOutcome ? ` / decision=${edge.decisionOutcome}` : ""}
            {edge.taskOutcome ? ` / task=${edge.taskOutcome}` : ""}
          </Tag>
        ))}
      </Card>
    </Space>
  );
}

function SecurityEditor() {
  const app = useMendixStudioStore(state => state.appSchema);
  const security = app.security;
  return (
    <Space vertical style={{ width: "100%" }}>
      <Card title="Security Notes">
        <ul style={{ margin: 0, paddingInlineStart: 16 }}>
          <li>Page Access 只控制页面访问，不等于数据安全。</li>
          <li>Microflow Access 只控制客户端入口执行，不等于实体数据权限。</li>
          <li>Entity Access 才是数据读写核心。</li>
          <li>后端 Runtime 必须强校验，不能只靠前端隐藏。</li>
        </ul>
      </Card>
      <Card title="User Roles">
        {security.userRoles.map(role => (
          <Tag key={role.roleId}>{role.name}</Tag>
        ))}
      </Card>
      <Card title="Module Roles">
        {security.moduleRoles.map(role => (
          <Tag key={role.roleId}>{role.name}</Tag>
        ))}
      </Card>
      <Card title="Page Access Matrix">
        {security.pageAccessRules.map(rule => (
          <Text key={rule.pageRef.id}>{rule.pageRef.id}: {rule.roleRefs.map(role => role.id).join(", ")}</Text>
        ))}
      </Card>
      <Card title="Microflow Access Matrix">
        {security.microflowAccessRules.map(rule => (
          <Text key={rule.microflowRef.id}>{rule.microflowRef.id}: {rule.roleRefs.map(role => role.id).join(", ")}</Text>
        ))}
      </Card>
      <Card title="Entity Access Matrix">
        {security.entityAccessRules.map(rule => (
          <Card key={rule.ruleId} size="small">
            <Space vertical style={{ width: "100%" }}>
              <Text>Rule: {rule.ruleId}</Text>
              <Input value={rule.xpathConstraint ?? ""} readOnly />
              {rule.memberAccess.map(access => (
                <Text key={access.attributeRef.id}>{access.attributeRef.id} / R:{String(access.read)} W:{String(access.write)}</Text>
              ))}
            </Space>
          </Card>
        ))}
      </Card>
    </Space>
  );
}

function RuntimePreviewPane() {
  const app = useMendixStudioStore(state => state.appSchema);
  const runtimeObject = useMendixStudioStore(state => state.runtimeObject);
  const setRuntimeObject = useMendixStudioStore(state => state.setRuntimeObject);
  const setLatestActionResponse = useMendixStudioStore(state => state.setLatestActionResponse);
  const setLatestTrace = useMendixStudioStore(state => state.setLatestTrace);
  const latestActionResponse = useMendixStudioStore(state => state.latestActionResponse);
  const executor = useMemo(() => createRuntimeExecutor(), []);

  return (
    <Space vertical style={{ width: "100%" }}>
      <Card title="Runtime Model">
        <Space>
          <InputNumber
            value={Number(runtimeObject.Amount ?? 0)}
            onNumberChange={value => setRuntimeObject({ ...runtimeObject, Amount: Number(value ?? 0) })}
          />
          <Input
            value={String(runtimeObject.Reason ?? "")}
            onChange={value => setRuntimeObject({ ...runtimeObject, Reason: value })}
          />
          <Tag color="blue">Status: {String(runtimeObject.Status ?? "")}</Tag>
        </Space>
      </Card>
      <Card title="Runtime Renderer">
        <RuntimeRenderer
          app={app}
          pageId="page_purchase_request_edit"
          objectState={runtimeObject}
          executor={executor}
          onStateChange={setRuntimeObject}
          onActionResponse={response => {
            setLatestActionResponse(response);
            if (response.traceId) {
              setLatestTrace(executor.getTrace(response.traceId));
            }
          }}
        />
      </Card>
      {latestActionResponse ? (
        <Card title="Action Response">
          <pre style={{ margin: 0, whiteSpace: "pre-wrap" }}>{JSON.stringify(latestActionResponse, null, 2)}</pre>
        </Card>
      ) : null}
    </Space>
  );
}

function PropertiesPane() {
  const selectedKind = useMendixStudioStore(state => state.selectedKind);
  const selectedId = useMendixStudioStore(state => state.selectedId);
  const appSchema = useMendixStudioStore(state => state.appSchema);
  return (
    <Card title="Properties">
      <Text>Selected kind: {selectedKind ?? "-"}</Text>
      <Text>Selected id: {selectedId ?? "-"}</Text>
      <Divider />
      <Text type="tertiary">App Name</Text>
      <Input
        value={appSchema.name}
        onChange={value =>
          updateSchema(schema => {
            schema.name = value;
          })
        }
      />
    </Card>
  );
}

function ErrorsPane() {
  const errors = useMendixStudioStore(state => state.validationErrors);
  return (
    <Card title={`Validation Errors (${errors.length})`} size="small">
      {errors.length === 0 ? (
        <Text type="success">No errors</Text>
      ) : (
        <Space vertical>
          {errors.map((error, index) => (
            <Text key={`${error.code}_${index}`} type={error.severity === "error" ? "danger" : "warning"}>
              [{error.code}] {error.message} ({error.target.kind}:{error.target.id})
            </Text>
          ))}
        </Space>
      )}
    </Card>
  );
}

function EditorWorkspace() {
  const activeTab = useMendixStudioStore(state => state.activeTab);
  if (activeTab === "domainModel") {
    return <DomainModelDesigner />;
  }
  if (activeTab === "pageBuilder") {
    return <PageBuilder />;
  }
  if (activeTab === "microflowDesigner") {
    return <MicroflowDesigner />;
  }
  if (activeTab === "workflowDesigner") {
    return <WorkflowDesigner />;
  }
  if (activeTab === "securityEditor") {
    return <SecurityEditor />;
  }
  return <RuntimePreviewPane />;
}

export function MendixStudioApp({ appId }: { appId?: string }) {
  const app = useMendixStudioStore(state => state.appSchema);
  const loadSampleApp = useMendixStudioStore(state => state.loadSampleApp);
  const setValidationErrors = useMendixStudioStore(state => state.setValidationErrors);
  const activeTab = useMendixStudioStore(state => state.activeTab);
  const [debugVisible, setDebugVisible] = useState(false);
  const latestTrace = useMendixStudioStore(state => state.latestTrace);

  return (
    <div style={{ height: "calc(100vh - 120px)", minHeight: 680, display: "grid", gridTemplateRows: "56px 1fr 180px", gap: 8 }}>
      <Card size="small">
        <Space>
          <Title heading={6} style={{ margin: 0 }}>{app.name}</Title>
          <Tag>{appId ?? app.appId}</Tag>
          <Button icon={<IconSave />} onClick={() => Toast.success("Schema saved in memory")}>保存</Button>
          <Button
            icon={<IconArrowRight />}
            onClick={() => {
              const errors = validateLowCodeAppSchema(app);
              setValidationErrors(errors);
              Toast.info(`校验完成，${errors.length} 条结果`);
            }}
          >
            校验
          </Button>
          <Button onClick={() => useMendixStudioStore.getState().setActiveTab("runtimePreview")}>预览</Button>
          <Button onClick={loadSampleApp}>示例数据加载</Button>
          <Button onClick={() => setDebugVisible(true)}>Debug Trace</Button>
          <Tag color="purple">{activeTab}</Tag>
        </Space>
      </Card>

      <div style={{ display: "grid", gridTemplateColumns: "260px 1fr 320px", gap: 8, minHeight: 0 }}>
        <Card title="App Explorer" bodyStyle={{ overflow: "auto", maxHeight: "100%" }}>
          <AppExplorer />
        </Card>
        <Card title="Workspace" bodyStyle={{ overflow: "auto", maxHeight: "100%" }}>
          <EditorWorkspace />
        </Card>
        <PropertiesPane />
      </div>

      <ErrorsPane />

      <SideSheet visible={debugVisible} title="Debug Trace Drawer" width={680} onCancel={() => setDebugVisible(false)}>
        <DebugTracePanel trace={latestTrace} />
      </SideSheet>
    </div>
  );
}

export function MendixStudioIndexPage({
  workspaceId,
  onOpen
}: {
  workspaceId: string;
  onOpen: (appId: string) => void;
}) {
  return (
    <Card title="Mendix Studio">
      <Space vertical style={{ width: "100%" }}>
        <Text type="tertiary">workspace: {workspaceId}</Text>
        <Button theme="solid" type="primary" onClick={() => onOpen("app_procurement")}>
          打开采购审批示例
        </Button>
        <Text>入口已挂载在资源中心微流 Tab 左侧，可直接跳转。</Text>
      </Space>
    </Card>
  );
}

export { useMendixStudioStore };
export { SAMPLE_PROCUREMENT_APP } from "./sample-app";
