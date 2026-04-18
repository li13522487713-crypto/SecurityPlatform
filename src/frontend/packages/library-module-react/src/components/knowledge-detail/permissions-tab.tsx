import { useEffect, useMemo, useState } from "react";
import {
  Banner,
  Button,
  Checkbox,
  Empty,
  Input,
  Select,
  SideSheet,
  Space,
  Table,
  Tag,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { IconDelete, IconPlus } from "@douyinfe/semi-icons";
import type {
  KnowledgeBaseDto,
  KnowledgePermission,
  KnowledgePermissionAction,
  KnowledgePermissionScope,
  KnowledgePermissionSubjectType,
  LibraryKnowledgeApi,
  SupportedLocale
} from "../../types";
import { getLibraryCopy } from "../../copy";
import { formatDateTime } from "../../utils";

export interface PermissionsTabProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  knowledge: KnowledgeBaseDto;
}

const ACTION_VALUES: KnowledgePermissionAction[] = ["view", "edit", "delete", "publish", "manage", "retrieve"];

export function PermissionsTab({ api, locale, knowledge }: PermissionsTabProps) {
  const copy = getLibraryCopy(locale);
  const [items, setItems] = useState<KnowledgePermission[]>([]);
  const [loading, setLoading] = useState(false);
  const [createVisible, setCreateVisible] = useState(false);
  const [scope, setScope] = useState<KnowledgePermissionScope>("kb");
  const [subjectType, setSubjectType] = useState<KnowledgePermissionSubjectType>("role");
  const [subjectId, setSubjectId] = useState("");
  const [subjectName, setSubjectName] = useState("");
  const [actions, setActions] = useState<KnowledgePermissionAction[]>(["view", "retrieve"]);
  // v5 §39 / 计划 G8：scope=document 时需要指定 documentId
  const [documentId, setDocumentId] = useState<string>("");
  const [documents, setDocuments] = useState<Array<{ id: number; fileName: string }>>([]);

  useEffect(() => {
    let cancelled = false;
    async function loadDocs() {
      try {
        const response = await api.listDocuments(knowledge.id, 1, 100);
        if (!cancelled) {
          setDocuments(response.items.map(d => ({ id: d.id, fileName: d.fileName })));
        }
      } catch {
        if (!cancelled) setDocuments([]);
      }
    }
    void loadDocs();
    return () => { cancelled = true; };
  }, [api, knowledge.id]);

  async function refresh() {
    if (!api.listPermissions) return;
    setLoading(true);
    try {
      const response = await api.listPermissions(knowledge.id, { pageIndex: 1, pageSize: 100 });
      setItems(response.items);
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
    return undefined;
  }, [knowledge.id]);

  const scopeLabel = useMemo<Record<KnowledgePermissionScope, string>>(() => ({
    space: copy.permissionsScopeSpace,
    project: copy.permissionsScopeProject,
    kb: copy.permissionsScopeKb,
    document: copy.permissionsScopeDocument
  }), [copy]);

  const actionLabel = useMemo<Record<KnowledgePermissionAction, string>>(() => ({
    view: copy.permissionsActionView,
    edit: copy.permissionsActionEdit,
    delete: copy.permissionsActionDelete,
    publish: copy.permissionsActionPublish,
    manage: copy.permissionsActionManage,
    retrieve: copy.permissionsActionRetrieve
  }), [copy]);

  const columns = useMemo<ColumnProps<KnowledgePermission>[]>(() => [
    {
      title: copy.permissionsScope,
      dataIndex: "scope",
      width: 100,
      render: (value: unknown) => <Tag color="violet">{scopeLabel[value as KnowledgePermissionScope]}</Tag>
    },
    {
      title: copy.permissionsSubject,
      dataIndex: "subjectName",
      render: (_value: unknown, record: KnowledgePermission) => (
        <div>
          <div style={{ fontWeight: 600 }}>{record.subjectName}</div>
          <Typography.Text type="tertiary" size="small">
            {record.subjectType} · {record.subjectId}
          </Typography.Text>
        </div>
      )
    },
    {
      title: copy.permissionsActions,
      dataIndex: "actions",
      render: (_value: unknown, record: KnowledgePermission) => (
        <Space wrap spacing={4}>
          {record.actions.map(action => (
            <Tag key={action} color="cyan" size="small">{actionLabel[action]}</Tag>
          ))}
        </Space>
      )
    },
    {
      title: copy.updatedAt,
      dataIndex: "grantedAt",
      width: 180,
      render: (value: unknown) => formatDateTime(typeof value === "string" ? value : undefined)
    },
    {
      title: copy.actions,
      width: 130,
      render: (_value: unknown, record: KnowledgePermission) => (
        <Button
          theme="borderless"
          type="danger"
          icon={<IconDelete />}
          onClick={async () => {
            if (!api.revokePermission) return;
            try {
              await api.revokePermission(knowledge.id, record.id);
              await refresh();
            } catch (error) {
              Toast.error((error as Error).message);
            }
          }}
        >
          {copy.permissionsRevoke}
        </Button>
      )
    }
  ], [actionLabel, api, copy, knowledge.id, scopeLabel]);

  async function handleCreate() {
    if (!api.grantPermission) return;
    if (!subjectId.trim() || !subjectName.trim()) {
      Toast.warning(copy.permissionsSubject);
      return;
    }
    if (scope === "document" && !documentId) {
      Toast.warning(copy.permissionsSelectDocumentRequired);
      return;
    }
    try {
      const docId = scope === "document" ? Number(documentId) : undefined;
      await api.grantPermission(knowledge.id, {
        scope,
        scopeId: scope === "kb"
          ? String(knowledge.id)
          : scope === "space"
            ? (knowledge.workspaceId ?? "ws-default")
            : scope === "document"
              ? String(docId ?? knowledge.id)
              : "project-default",
        knowledgeBaseId: knowledge.id,
        documentId: docId,
        subjectType,
        subjectId: subjectId.trim(),
        subjectName: subjectName.trim(),
        actions
      });
      setCreateVisible(false);
      setSubjectId("");
      setSubjectName("");
      setDocumentId("");
      await refresh();
    } catch (error) {
      Toast.error((error as Error).message);
    }
  }

  return (
    <div className="atlas-table-card semi-card semi-card-bordered semi-card-shadow">
      <div className="semi-card-header">
        <div className="semi-card-header-wrapper">
          <div>
            <Typography.Title heading={5} style={{ margin: 0 }}>{copy.permissionsTitle}</Typography.Title>
            <Typography.Text type="tertiary">{copy.permissionsSubtitle}</Typography.Text>
          </div>
          <Button type="primary" icon={<IconPlus />} onClick={() => setCreateVisible(true)}>
            {copy.permissionsAddTitle}
          </Button>
        </div>
      </div>
      <div className="semi-card-body" style={{ padding: 0 }}>
        {items.length === 0 ? (
          <div style={{ padding: 32 }}>
            <Empty description={copy.permissionsEmpty} />
          </div>
        ) : (
          <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />
        )}
      </div>

      {/* v5 §39 / 计划 G8：Modal 改为 SideSheet 风格，支持 document scope 选择 documentId */}
      <SideSheet
        title={copy.permissionsAddTitle}
        visible={createVisible}
        onCancel={() => setCreateVisible(false)}
        width={520}
        footer={
          <Space>
            <Button onClick={() => setCreateVisible(false)}>{copy.cancel}</Button>
            <Button type="primary" onClick={handleCreate}>{copy.create}</Button>
          </Space>
        }
      >
        <Space vertical align="start" style={{ width: "100%" }}>
          <Banner type="info" description={copy.permissionsSubtitle} />
          <Typography.Text strong>{copy.permissionsScope}</Typography.Text>
          <Select
            value={scope}
            style={{ width: "100%" }}
            onChange={value => setScope(value as KnowledgePermissionScope)}
            optionList={[
              { label: copy.permissionsScopeSpace, value: "space" },
              { label: copy.permissionsScopeProject, value: "project" },
              { label: copy.permissionsScopeKb, value: "kb" },
              { label: copy.permissionsScopeDocument, value: "document" }
            ]}
          />
          {scope === "document" ? (
            <>
              <Typography.Text strong>Document</Typography.Text>
              <Select
                value={documentId}
                style={{ width: "100%" }}
                onChange={value => setDocumentId(value as string)}
                optionList={documents.map(d => ({ label: `#${d.id} ${d.fileName}`, value: String(d.id) }))}
                placeholder={copy.permissionsSelectDocumentPlaceholder}
              />
            </>
          ) : null}
          <Typography.Text strong>{copy.permissionsSubject}</Typography.Text>
          <Select
            value={subjectType}
            style={{ width: "100%" }}
            onChange={value => setSubjectType(value as KnowledgePermissionSubjectType)}
            optionList={[
              { label: "User", value: "user" },
              { label: "Role", value: "role" },
              { label: "Group", value: "group" }
            ]}
          />
          <Input value={subjectName} placeholder="subject name" onChange={value => setSubjectName(value)} />
          <Input value={subjectId} placeholder="subject id" onChange={value => setSubjectId(value)} />
          <Typography.Text strong>{copy.permissionsActions}</Typography.Text>
          <Checkbox.Group
            value={actions}
            options={ACTION_VALUES.map(action => ({
              label: actionLabel[action],
              value: action
            }))}
            onChange={(values: unknown) => setActions((values as KnowledgePermissionAction[]) ?? [])}
          />
        </Space>
      </SideSheet>
    </div>
  );
}
