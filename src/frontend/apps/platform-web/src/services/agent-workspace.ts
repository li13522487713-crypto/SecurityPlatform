import type {
  AgentListItem,
  MultiAgentExecutionStatus,
  MultiAgentOrchestrationListItem,
  MultiAgentOrchestrationMode
} from "@/services/api-ai";

export type WorkspaceAgentType = "single" | "team";
export type WorkspaceTeamMode = "group_chat" | "workflow" | "handoff";
export type WorkspaceAgentStatus = "Draft" | "Published" | "Disabled";
export type WorkspaceAgentCapability = "chat" | "knowledge" | "automation" | "schema_builder" | "ops";

export interface WorkspaceAgentCard {
  id: string;
  sourceId: string | number;
  agentType: WorkspaceAgentType;
  teamMode?: WorkspaceTeamMode;
  name: string;
  description?: string;
  status: WorkspaceAgentStatus;
  modelName?: string;
  capabilityTags: WorkspaceAgentCapability[];
  memberCount: number;
  memberNames: string[];
  defaultEntrySkill: WorkspaceAgentCapability;
  lastRunStatus?: MultiAgentExecutionStatus;
  publishedVersion: number;
  boundDataAssets: string[];
  updatedAt: string;
  createdAt?: string;
  rawAgent?: AgentListItem;
  rawTeam?: MultiAgentOrchestrationListItem;
}

export interface SchemaDraftEntity {
  name: string;
  title: string;
  description: string;
}

export interface SchemaDraftField {
  entityName: string;
  name: string;
  title: string;
  type: string;
  required: boolean;
  description: string;
}

export interface SchemaDraftRelation {
  from: string;
  to: string;
  type: string;
  description: string;
}

export interface SchemaDraftIndex {
  entityName: string;
  name: string;
  columns: string[];
  unique: boolean;
  reason: string;
}

export interface SchemaDraftSecurityPolicy {
  name: string;
  description: string;
}

export interface SchemaDraft {
  title: string;
  summary: string;
  entities: SchemaDraftEntity[];
  fields: SchemaDraftField[];
  relations: SchemaDraftRelation[];
  indexes: SchemaDraftIndex[];
  securityPolicies: SchemaDraftSecurityPolicy[];
  openQuestions: string[];
  confirmationState: "draft" | "ready";
}

export interface TeamAgentMetadata {
  mode: WorkspaceTeamMode;
  coordinatorName: string;
  coordinatorPrompt: string;
  relationshipMode: string;
  stopCondition: string;
  capabilityFlags: {
    schemaBuilder: boolean;
    fieldSuggestions: boolean;
    indexSuggestions: boolean;
    permissionSuggestions: boolean;
  };
  schemaDraft?: SchemaDraft;
}

const TEAM_AGENT_PREFIX = "team-";
const TEAM_AGENT_META_KEY = "atlas_team_agent_meta_";

export function isTeamAgentId(id: string) {
  return id.startsWith(TEAM_AGENT_PREFIX);
}

export function getTeamAgentSourceId(id: string) {
  return Number(id.slice(TEAM_AGENT_PREFIX.length));
}

export function getWorkspaceAgentId(type: WorkspaceAgentType, sourceId: string | number) {
  return type === "team" ? `${TEAM_AGENT_PREFIX}${sourceId}` : String(sourceId);
}

export function toWorkspaceTeamMode(mode: MultiAgentOrchestrationMode): WorkspaceTeamMode {
  return mode === 1 ? "workflow" : "group_chat";
}

export function toOrchestrationMode(mode: WorkspaceTeamMode): MultiAgentOrchestrationMode {
  return mode === "workflow" ? 1 : 0;
}

export function toWorkspaceStatus(status: string): WorkspaceAgentStatus {
  if (status === "Published") {
    return "Published";
  }

  if (status === "Disabled") {
    return "Disabled";
  }

  return "Draft";
}

export function toTeamWorkspaceStatus(status: number): WorkspaceAgentStatus {
  if (status === 1) {
    return "Published";
  }

  if (status === 2) {
    return "Disabled";
  }

  return "Draft";
}

export function getDefaultTeamAgentMetadata(name: string, description?: string): TeamAgentMetadata {
  const mode = inferTeamMode(name, description);
  const schemaBuilder = inferCapabilityTags(name, description).includes("schema_builder");
  return {
    mode,
    coordinatorName: mode === "group_chat" ? "Coordinator" : "Lead Agent",
    coordinatorPrompt: schemaBuilder
      ? "负责协调业务分析、数据建模、接口设计与权限策略，输出结构化建表草案。"
      : "负责协调团队成员分工，汇总中间结论并形成最终结果。",
    relationshipMode: mode === "handoff" ? "handoff" : mode === "workflow" ? "dag" : "round_robin",
    stopCondition: schemaBuilder ? "approved_or_manual" : "max_round_or_manual",
    capabilityFlags: {
      schemaBuilder,
      fieldSuggestions: schemaBuilder,
      indexSuggestions: schemaBuilder,
      permissionSuggestions: schemaBuilder
    }
  };
}

export function loadTeamAgentMetadata(teamId: number, name: string, description?: string): TeamAgentMetadata {
  if (typeof localStorage === "undefined") {
    return getDefaultTeamAgentMetadata(name, description);
  }

  const raw = localStorage.getItem(`${TEAM_AGENT_META_KEY}${teamId}`);
  if (!raw) {
    return getDefaultTeamAgentMetadata(name, description);
  }

  try {
    return {
      ...getDefaultTeamAgentMetadata(name, description),
      ...(JSON.parse(raw) as TeamAgentMetadata)
    };
  } catch {
    return getDefaultTeamAgentMetadata(name, description);
  }
}

export function saveTeamAgentMetadata(teamId: number, metadata: TeamAgentMetadata) {
  if (typeof localStorage === "undefined") {
    return;
  }

  localStorage.setItem(`${TEAM_AGENT_META_KEY}${teamId}`, JSON.stringify(metadata));
}

export function inferTeamMode(name: string, description?: string): WorkspaceTeamMode {
  const text = `${name} ${description || ""}`.toLowerCase();
  if (text.includes("workflow") || text.includes("流程")) {
    return "workflow";
  }

  if (text.includes("handoff") || text.includes("接力")) {
    return "handoff";
  }

  return "group_chat";
}

export function inferCapabilityTags(name: string, description?: string): WorkspaceAgentCapability[] {
  const text = `${name} ${description || ""}`.toLowerCase();
  const tags = new Set<WorkspaceAgentCapability>();
  tags.add("chat");

  if (text.includes("知识") || text.includes("rag") || text.includes("knowledge")) {
    tags.add("knowledge");
  }

  if (
    text.includes("数据") ||
    text.includes("表") ||
    text.includes("schema") ||
    text.includes("erd") ||
    text.includes("db")
  ) {
    tags.add("schema_builder");
  }

  if (text.includes("巡检") || text.includes("安全") || text.includes("ops") || text.includes("运维")) {
    tags.add("ops");
  }

  if (text.includes("工作流") || text.includes("自动") || text.includes("automation")) {
    tags.add("automation");
  }

  return Array.from(tags);
}

export function buildWorkspaceAgentCards(
  agents: AgentListItem[],
  teams: MultiAgentOrchestrationListItem[]
): WorkspaceAgentCard[] {
  const agentLookup = new Map(agents.map((item) => [String(item.id), item.name]));
  const singles = agents.map<WorkspaceAgentCard>((item) => {
    const capabilityTags = inferCapabilityTags(item.name, item.description);
    return {
      id: getWorkspaceAgentId("single", item.id),
      sourceId: item.id,
      agentType: "single",
      name: item.name,
      description: item.description,
      status: toWorkspaceStatus(item.status),
      modelName: item.modelName,
      capabilityTags,
      memberCount: 1,
      memberNames: [item.name],
      defaultEntrySkill: capabilityTags.includes("schema_builder") ? "schema_builder" : "chat",
      publishedVersion: item.publishVersion,
      boundDataAssets: capabilityTags.includes("schema_builder") ? ["ERD", "Schema"] : [],
      updatedAt: item.createdAt,
      createdAt: item.createdAt,
      rawAgent: item
    };
  });

  const teamCards = teams.map<WorkspaceAgentCard>((item) => {
    const metadata = loadTeamAgentMetadata(item.id, item.name, item.description);
    const capabilityTags = inferCapabilityTags(item.name, item.description);
    if (metadata.capabilityFlags.schemaBuilder) {
      capabilityTags.push("schema_builder");
    }

    const memberNames = new Array(Math.max(item.memberCount, 0))
      .fill("")
      .map((_, index) => agentLookup.get(String(index + 1)))
      .filter((value): value is string => Boolean(value));

    return {
      id: getWorkspaceAgentId("team", item.id),
      sourceId: item.id,
      agentType: "team",
      teamMode: metadata.mode || toWorkspaceTeamMode(item.mode),
      name: item.name,
      description: item.description,
      status: toTeamWorkspaceStatus(item.status),
      capabilityTags: Array.from(new Set(capabilityTags)),
      memberCount: item.memberCount,
      memberNames,
      defaultEntrySkill: metadata.capabilityFlags.schemaBuilder ? "schema_builder" : "automation",
      publishedVersion: item.status === 1 ? 1 : 0,
      boundDataAssets: metadata.capabilityFlags.schemaBuilder ? ["Schema Draft", "Security Policy"] : [],
      updatedAt: item.updatedAt,
      createdAt: item.createdAt,
      rawTeam: item
    };
  });

  return [...teamCards, ...singles].sort(
    (left, right) => new Date(right.updatedAt).getTime() - new Date(left.updatedAt).getTime()
  );
}

export function buildWorkspaceActivities(cards: WorkspaceAgentCard[]) {
  return cards.slice(0, 6).map((item, index) => {
    const capability = item.capabilityTags.includes("schema_builder") ? "schema_builder" : item.defaultEntrySkill;
    return {
      id: `${item.id}-${index}`,
      agentId: item.id,
      title:
        capability === "schema_builder"
          ? "schema_draft_created"
          : item.agentType === "team"
            ? "team_run_finished"
            : "agent_updated",
      updatedAt: item.updatedAt,
      agentName: item.name
    };
  });
}

export function buildSchemaDraftFromPrompt(prompt: string): SchemaDraft {
  const normalized = prompt.trim();
  const entities = inferEntities(normalized);
  const fields = entities.flatMap((entity) => createDefaultFields(entity.name));
  const relations = createDefaultRelations(entities.map((item) => item.name));
  const indexes = createDefaultIndexes(entities.map((item) => item.name));

  return {
    title: entities.length > 0 ? `${entities[0].title}数据草案` : "业务数据草案",
    summary: normalized || "基于团队协作生成的业务数据结构草案",
    entities,
    fields,
    relations,
    indexes,
    securityPolicies: [
      { name: "tenant_isolation", description: "所有业务表增加 tenant_id，默认按租户隔离。" },
      { name: "audit_columns", description: "保留 created_at、created_by、updated_at、updated_by 审计字段。" },
      { name: "owner_scope", description: "建议按部门、负责人和角色增加访问范围控制。" }
    ],
    openQuestions: [
      "是否需要软删除标记与归档状态？",
      "是否存在跨租户共享数据场景？",
      "附件、审批记录是否需要拆成独立从表？"
    ],
    confirmationState: "draft"
  };
}

function inferEntities(prompt: string) {
  const pairs = [
    { key: "合同", name: "contract", title: "合同", description: "记录合同主体、金额、状态与签约信息。" },
    { key: "客户", name: "customer", title: "客户", description: "记录客户主体、联系方式与归属信息。" },
    { key: "回款", name: "payment", title: "回款", description: "记录回款节点、金额与到账状态。" },
    { key: "发票", name: "invoice", title: "发票", description: "记录发票抬头、金额与开票状态。" },
    { key: "订单", name: "order", title: "订单", description: "记录订单主数据与业务流转状态。" },
    { key: "项目", name: "project", title: "项目", description: "记录项目周期、负责人和执行状态。" }
  ];

  const matched = pairs.filter((item) => prompt.includes(item.key));
  if (matched.length > 0) {
    return matched;
  }

  return [
    {
      name: "business_record",
      title: "业务主表",
      description: "记录当前业务对象的核心字段、状态与负责人。"
    }
  ];
}

function createDefaultFields(entityName: string): SchemaDraftField[] {
  const baseFields: SchemaDraftField[] = [
    { entityName, name: "id", title: "主键", type: "bigint", required: true, description: "主键 ID" },
    { entityName, name: "tenant_id", title: "租户", type: "guid", required: true, description: "租户隔离字段" },
    { entityName, name: "code", title: "编码", type: "nvarchar(64)", required: true, description: "业务编码" },
    { entityName, name: "name", title: "名称", type: "nvarchar(128)", required: true, description: "业务名称" },
    { entityName, name: "status", title: "状态", type: "nvarchar(32)", required: true, description: "业务状态" },
    { entityName, name: "owner_user_id", title: "负责人", type: "guid", required: false, description: "当前负责人" },
    { entityName, name: "created_at", title: "创建时间", type: "datetime", required: true, description: "创建时间" },
    { entityName, name: "updated_at", title: "更新时间", type: "datetime", required: true, description: "更新时间" }
  ];

  if (entityName === "contract") {
    baseFields.push(
      { entityName, name: "customer_id", title: "客户", type: "bigint", required: true, description: "关联客户主表" },
      { entityName, name: "amount", title: "合同金额", type: "decimal(18,2)", required: true, description: "合同总金额" }
    );
  }

  if (entityName === "payment") {
    baseFields.push(
      { entityName, name: "contract_id", title: "合同", type: "bigint", required: true, description: "关联合同主表" },
      { entityName, name: "paid_amount", title: "回款金额", type: "decimal(18,2)", required: true, description: "实际到账金额" }
    );
  }

  return baseFields;
}

function createDefaultRelations(entityNames: string[]): SchemaDraftRelation[] {
  const relations: SchemaDraftRelation[] = [];
  if (entityNames.includes("customer") && entityNames.includes("contract")) {
    relations.push({
      from: "customer",
      to: "contract",
      type: "1:N",
      description: "一个客户可以拥有多个合同。"
    });
  }

  if (entityNames.includes("contract") && entityNames.includes("payment")) {
    relations.push({
      from: "contract",
      to: "payment",
      type: "1:N",
      description: "一个合同可以有多次回款记录。"
    });
  }

  return relations;
}

function createDefaultIndexes(entityNames: string[]): SchemaDraftIndex[] {
  return entityNames.map((entityName) => ({
    entityName,
    name: `idx_${entityName}_tenant_status`,
    columns: ["tenant_id", "status"],
    unique: false,
    reason: "支撑租户隔离与列表检索。"
  }));
}
