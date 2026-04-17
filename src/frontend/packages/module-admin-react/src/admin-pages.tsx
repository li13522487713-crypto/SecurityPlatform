import { useDeferredValue, useEffect, useState, startTransition } from "react";
import type { ReactNode } from "react";
import {
  Banner,
  Button,
  Empty,
  Input,
  Modal,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import { IconDelete, IconEdit, IconPlus, IconRefresh, IconSearch } from "@douyinfe/semi-icons";
import type {
  AdminLocale,
  AdminPageCommonProps,
  OrganizationOverview,
  OrganizationOverviewItem
} from "./types";
import type {
  DepartmentListItem,
  PositionCreateRequest,
  PositionListItem,
  RoleCreateRequest,
  RoleListItem,
  RoleUpdateRequest,
  UserCreateRequest,
  UserListItem,
  UserUpdateRequest
} from "@atlas/shared-react-core/types";

interface CopyMap {
  overviewTitle: string;
  overviewSubtitle: string;
  searchPlaceholder: string;
  create: string;
  save: string;
  cancel: string;
  refresh: string;
  edit: string;
  delete: string;
  action: string;
  empty: string;
  confirmDelete: string;
  loadFailed: string;
  saveFailed: string;
  deleteFailed: string;
  saved: string;
  deleted: string;
  usersTitle: string;
  usersSubtitle: string;
  rolesTitle: string;
  rolesSubtitle: string;
  departmentsTitle: string;
  departmentsSubtitle: string;
  positionsTitle: string;
  positionsSubtitle: string;
  username: string;
  password: string;
  displayName: string;
  email: string;
  phone: string;
  code: string;
  name: string;
  description: string;
  status: string;
}

const copy: Record<AdminLocale, CopyMap> = {
  "zh-CN": {
    overviewTitle: "组织概览",
    overviewSubtitle: "把用户、角色、部门和岗位统一收拢到 Coze 壳内的组织入口。",
    searchPlaceholder: "搜索名称、编码或关键词",
    create: "创建",
    save: "保存",
    cancel: "取消",
    refresh: "刷新",
    edit: "编辑",
    delete: "删除",
    action: "操作",
    empty: "暂无数据",
    confirmDelete: "确认删除该条记录吗？",
    loadFailed: "加载失败",
    saveFailed: "保存失败",
    deleteFailed: "删除失败",
    saved: "保存成功",
    deleted: "删除成功",
    usersTitle: "用户管理",
    usersSubtitle: "在应用级维护成员账号、基本信息和访问配置。",
    rolesTitle: "角色管理",
    rolesSubtitle: "按 Coze 风格列表和侧边编辑方式维护应用角色。",
    departmentsTitle: "部门管理",
    departmentsSubtitle: "组织结构统一通过应用宿主接口维护。",
    positionsTitle: "职位管理",
    positionsSubtitle: "支持职位编制、排序和描述维护。",
    username: "用户名",
    password: "密码",
    displayName: "显示名称",
    email: "邮箱",
    phone: "手机号",
    code: "编码",
    name: "名称",
    description: "描述",
    status: "状态"
  },
  "en-US": {
    overviewTitle: "Organization Overview",
    overviewSubtitle: "A Coze-style landing page for users, roles, departments, and positions.",
    searchPlaceholder: "Search by name, code, or keyword",
    create: "Create",
    save: "Save",
    cancel: "Cancel",
    refresh: "Refresh",
    edit: "Edit",
    delete: "Delete",
    action: "Action",
    empty: "No data",
    confirmDelete: "Delete this record?",
    loadFailed: "Load failed",
    saveFailed: "Save failed",
    deleteFailed: "Delete failed",
    saved: "Saved",
    deleted: "Deleted",
    usersTitle: "Users",
    usersSubtitle: "Manage member accounts and profile settings at app scope.",
    rolesTitle: "Roles",
    rolesSubtitle: "Maintain app roles with a Coze-style list and edit flow.",
    departmentsTitle: "Departments",
    departmentsSubtitle: "Maintain organization structure through AppHost APIs.",
    positionsTitle: "Positions",
    positionsSubtitle: "Manage positions, order, and descriptions.",
    username: "Username",
    password: "Password",
    displayName: "Display Name",
    email: "Email",
    phone: "Phone",
    code: "Code",
    name: "Name",
    description: "Description",
    status: "Status"
  }
};

interface Column<T> {
  key: string;
  title: string;
  render(item: T): ReactNode;
}

function useCopy(locale: AdminLocale): CopyMap {
  return copy[locale];
}

function PageShell({
  title,
  subtitle,
  toolbar,
  testId,
  children
}: {
  title: string;
  subtitle: string;
  toolbar?: ReactNode;
  testId: string;
  children: ReactNode;
}) {
  return (
    <section className="module-admin__page" data-testid={testId}>
      <div className="module-admin__page-header">
        <div>
          <Typography.Title heading={4} style={{ margin: 0 }}>{title}</Typography.Title>
          <Typography.Text type="tertiary">{subtitle}</Typography.Text>
        </div>
        {toolbar ? <div className="module-admin__toolbar">{toolbar}</div> : null}
      </div>
      <div className="module-admin__surface">{children}</div>
    </section>
  );
}

function SearchToolbar({
  value,
  onChange,
  onRefresh,
  onCreate,
  placeholder,
  createLabel,
  refreshLabel,
  createTestId
}: {
  value: string;
  onChange: (value: string) => void;
  onRefresh: () => void;
  onCreate?: () => void;
  placeholder: string;
  createLabel: string;
  refreshLabel: string;
  createTestId?: string;
}) {
  return (
    <>
      <Input
        value={value}
        onChange={nextValue => startTransition(() => onChange(nextValue))}
        prefix={<IconSearch />}
        placeholder={placeholder}
        className="module-admin__search"
      />
      <Button icon={<IconRefresh />} theme="borderless" onClick={onRefresh}>
        {refreshLabel}
      </Button>
      {onCreate ? (
        <Button
          icon={<IconPlus />}
          theme="solid"
          type="primary"
          onClick={onCreate}
          {...(createTestId ? { ["data-testid"]: createTestId } : {})}
        >
          {createLabel}
        </Button>
      ) : null}
    </>
  );
}

function DataTable<T>({
  testId,
  columns,
  items,
  emptyText
}: {
  testId: string;
  columns: Column<T>[];
  items: T[];
  emptyText: string;
}) {
  return (
    <div className="module-admin__table-wrap" data-testid={testId}>
      <table className="module-admin__table">
        <thead>
          <tr>
            {columns.map(column => <th key={column.key}>{column.title}</th>)}
          </tr>
        </thead>
        <tbody>
          {items.length === 0 ? (
            <tr>
              <td colSpan={columns.length}>
                <Empty title={emptyText} image={null} />
              </td>
            </tr>
          ) : (
            items.map((item, index) => (
              <tr key={index}>
                {columns.map(column => <td key={column.key}>{column.render(item)}</td>)}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}

function useKeyword(initialValue = "") {
  const [keyword, setKeyword] = useState(initialValue);
  const deferredKeyword = useDeferredValue(keyword);
  return { keyword, deferredKeyword, setKeyword };
}

function OverviewMetric({
  title,
  value,
  subtitle
}: {
  title: string;
  value: number;
  subtitle: string;
}) {
  return (
    <article className="module-admin__overview-metric">
      <span className="module-admin__overview-metric-label">{title}</span>
      <strong>{value}</strong>
      <p>{subtitle}</p>
    </article>
  );
}

function OverviewList({
  title,
  items,
  emptyTitle
}: {
  title: string;
  items: OrganizationOverviewItem[];
  emptyTitle: string;
}) {
  return (
    <section className="module-admin__overview-panel">
      <Typography.Title heading={6} style={{ margin: 0 }}>{title}</Typography.Title>
      {items.length === 0 ? (
        <Empty title={emptyTitle} image={null} />
      ) : (
        <div className="module-admin__overview-list">
          {items.map(item => (
            <article key={`${title}-${item.id}`} className="module-admin__overview-item">
              <div>
                <strong>{item.title}</strong>
                {item.subtitle ? <Typography.Text type="tertiary">{item.subtitle}</Typography.Text> : null}
              </div>
              {item.meta ? <span className="module-admin__meta">{item.meta}</span> : null}
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

export function OrganizationOverviewPage({ api, locale }: AdminPageCommonProps) {
  const text = useCopy(locale);
  const [overview, setOverview] = useState<OrganizationOverview | null>(null);
  const [loading, setLoading] = useState(false);

  const load = async () => {
    setLoading(true);
    try {
      setOverview(await api.getOrganizationOverview());
    } catch (error) {
      Toast.error((error as Error).message || text.loadFailed);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  return (
    <PageShell
      title={text.overviewTitle}
      subtitle={text.overviewSubtitle}
      testId="app-organization-overview-page"
      toolbar={<Button icon={<IconRefresh />} theme="borderless" onClick={() => void load()}>{text.refresh}</Button>}
    >
      {loading ? <Typography.Text type="tertiary">Loading...</Typography.Text> : null}
      <div className="module-admin__overview-grid">
        <OverviewMetric title={locale === "zh-CN" ? "成员" : "Members"} value={overview?.memberCount ?? 0} subtitle={locale === "zh-CN" ? "应用内已加入组织成员" : "Members in current app organization"} />
        <OverviewMetric title={locale === "zh-CN" ? "角色" : "Roles"} value={overview?.roleCount ?? 0} subtitle={locale === "zh-CN" ? "可分配的组织角色" : "Assignable app roles"} />
        <OverviewMetric title={locale === "zh-CN" ? "部门" : "Departments"} value={overview?.departmentCount ?? 0} subtitle={locale === "zh-CN" ? "组织结构节点" : "Organization units"} />
        <OverviewMetric title={locale === "zh-CN" ? "岗位" : "Positions"} value={overview?.positionCount ?? 0} subtitle={locale === "zh-CN" ? "岗位与职责编制" : "Position registry"} />
      </div>
      {(overview?.uncoveredMemberCount ?? 0) > 0 ? (
        <Banner
          type="warning"
          bordered={false}
          fullMode={false}
          title={locale === "zh-CN" ? "治理提醒" : "Governance Notice"}
          description={locale === "zh-CN"
            ? `仍有 ${overview?.uncoveredMemberCount ?? 0} 位成员未完成角色覆盖。`
            : `${overview?.uncoveredMemberCount ?? 0} members are still uncovered by role governance.`}
        />
      ) : null}
      <div className="module-admin__overview-columns">
        <OverviewList title={locale === "zh-CN" ? "最近成员" : "Recent Members"} items={overview?.recentMembers ?? []} emptyTitle={text.empty} />
        <OverviewList title={locale === "zh-CN" ? "最近角色" : "Recent Roles"} items={overview?.recentRoles ?? []} emptyTitle={text.empty} />
        <OverviewList title={locale === "zh-CN" ? "部门与岗位" : "Departments & Positions"} items={[...(overview?.recentDepartments ?? []), ...(overview?.recentPositions ?? [])]} emptyTitle={text.empty} />
      </div>
    </PageShell>
  );
}

export function UsersAdminPage({ api, locale }: AdminPageCommonProps) {
  const text = useCopy(locale);
  const { keyword, deferredKeyword, setKeyword } = useKeyword();
  const [items, setItems] = useState<UserListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [open, setOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState({
    username: "",
    password: "",
    displayName: "",
    email: "",
    phoneNumber: ""
  });

  const load = async () => {
    setLoading(true);
    try {
      const result = await api.listUsers({ pageIndex: 1, pageSize: 20, keyword: deferredKeyword });
      setItems(result.items);
    } catch (error) {
      Toast.error((error as Error).message || text.loadFailed);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, [deferredKeyword]);

  const beginCreate = () => {
    setEditingId(null);
    setForm({ username: "", password: "", displayName: "", email: "", phoneNumber: "" });
    setOpen(true);
  };

  const beginEdit = async (id: string) => {
    const detail = await api.getUserDetail(id);
    setEditingId(id);
    setForm({
      username: detail.username,
      password: "",
      displayName: detail.displayName,
      email: detail.email ?? "",
      phoneNumber: detail.phoneNumber ?? ""
    });
    setOpen(true);
  };

  const submit = async () => {
    try {
      if (editingId) {
        const request: UserUpdateRequest = {
          displayName: form.displayName.trim(),
          email: form.email.trim() || undefined,
          phoneNumber: form.phoneNumber.trim() || undefined,
          isActive: true
        };
        await api.updateUser(editingId, request);
      } else {
        const request: UserCreateRequest = {
          username: form.username.trim(),
          password: form.password,
          displayName: form.displayName.trim(),
          email: form.email.trim() || undefined,
          phoneNumber: form.phoneNumber.trim() || undefined,
          isActive: true,
          roleIds: [],
          departmentIds: [],
          positionIds: []
        };
        await api.createUser(request);
      }

      setOpen(false);
      await load();
    } catch (error) {
      Toast.error((error as Error).message || text.saveFailed);
    }
  };

  return (
    <>
      <PageShell
        title={text.usersTitle}
        subtitle={text.usersSubtitle}
        testId="app-users-page"
        toolbar={(
          <SearchToolbar
            value={keyword}
            onChange={setKeyword}
            onRefresh={() => void load()}
            onCreate={beginCreate}
            placeholder={text.searchPlaceholder}
            createLabel={text.create}
            refreshLabel={text.refresh}
            createTestId="app-users-create"
          />
        )}
      >
        {loading ? <Banner type="info" description="Loading..." /> : null}
        <DataTable
          testId="app-users-table"
          emptyText={text.empty}
          items={items}
          columns={[
            { key: "username", title: text.username, render: item => item.username },
            { key: "displayName", title: text.displayName, render: item => item.displayName },
            { key: "email", title: text.email, render: item => item.email || "-" },
            { key: "status", title: text.status, render: item => (item.isActive ? "Active" : "Inactive") },
            {
              key: "actions",
              title: text.action,
              render: item => (
                <div className="module-admin__actions">
                  <Button theme="borderless" icon={<IconEdit />} {...({ ["data-testid"]: `app-users-edit-${item.id}` })} onClick={() => void beginEdit(item.id)} />
                  <Button theme="borderless" icon={<IconDelete />} {...({ ["data-testid"]: `app-users-remove-${item.id}` })} onClick={() => void api.deleteUser(item.id).then(load)} />
                </div>
              )
            }
          ]}
        />
      </PageShell>

      <Modal
        title={editingId ? text.edit : text.create}
        visible={open}
        onCancel={() => setOpen(false)}
        onOk={() => void submit()}
        okText={text.save}
        cancelText={text.cancel}
      >
        {!editingId ? <Input value={form.username} onChange={value => setForm((current) => ({ ...current, username: value }))} data-testid="app-users-form-username" className="module-admin__field" /> : null}
        {!editingId ? <Input value={form.password} onChange={value => setForm((current) => ({ ...current, password: value }))} data-testid="app-users-form-password" className="module-admin__field" /> : null}
        <Input value={form.displayName} onChange={value => setForm((current) => ({ ...current, displayName: value }))} data-testid={editingId ? "app-users-edit-display-name" : "app-users-form-display-name"} className="module-admin__field" />
      </Modal>
    </>
  );
}

export function RolesAdminPage({ api, locale }: AdminPageCommonProps) {
  const text = useCopy(locale);
  const { keyword, deferredKeyword, setKeyword } = useKeyword();
  const [items, setItems] = useState<RoleListItem[]>([]);
  const [open, setOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState({ name: "", code: "", description: "" });

  const load = async () => {
    const result = await api.listRoles({ pageIndex: 1, pageSize: 20, keyword: deferredKeyword });
    setItems(result.items);
  };

  useEffect(() => {
    void load();
  }, [deferredKeyword]);

  const beginEdit = async (id: string) => {
    const detail = await api.getRoleDetail(id);
    setEditingId(id);
    setForm({ name: detail.name, code: detail.code, description: detail.description ?? "" });
    setOpen(true);
  };

  const submit = async () => {
    if (editingId) {
      const request: RoleUpdateRequest = { name: form.name.trim(), description: form.description.trim() || undefined };
      await api.updateRole(editingId, request);
    } else {
      const request: RoleCreateRequest = { name: form.name.trim(), code: form.code.trim(), description: form.description.trim() || undefined };
      await api.createRole(request);
    }
    setOpen(false);
    await load();
  };

  return (
    <>
      <PageShell
        title={text.rolesTitle}
        subtitle={text.rolesSubtitle}
        testId="app-roles-page"
        toolbar={(
          <SearchToolbar
            value={keyword}
            onChange={setKeyword}
            onRefresh={() => void load()}
            onCreate={() => {
              setEditingId(null);
              setForm({ name: "", code: "", description: "" });
              setOpen(true);
            }}
            placeholder={text.searchPlaceholder}
            createLabel={text.create}
            refreshLabel={text.refresh}
            createTestId="app-roles-create"
          />
        )}
      >
        <DataTable
          testId="app-roles-table"
          emptyText={text.empty}
          items={items}
          columns={[
            { key: "name", title: text.name, render: item => item.name },
            { key: "code", title: text.code, render: item => item.code },
            {
              key: "actions",
              title: text.action,
              render: item => (
                <div className="module-admin__actions">
                  <Button theme="borderless" icon={<IconEdit />} {...({ ["data-testid"]: `app-roles-edit-${item.id}` })} onClick={() => void beginEdit(item.id)} />
                  <Button theme="borderless" icon={<IconDelete />} {...({ ["data-testid"]: `app-roles-delete-${item.id}` })} onClick={() => void api.deleteRole(item.id).then(load)} />
                </div>
              )
            }
          ]}
        />
      </PageShell>

      <Modal title={editingId ? text.edit : text.create} visible={open} onCancel={() => setOpen(false)} onOk={() => void submit()} okText={text.save} cancelText={text.cancel}>
        <Input value={form.name} onChange={value => setForm((current) => ({ ...current, name: value }))} data-testid="app-roles-form-name" className="module-admin__field" />
        {!editingId ? <Input value={form.code} onChange={value => setForm((current) => ({ ...current, code: value }))} data-testid="app-roles-form-code" className="module-admin__field" /> : null}
      </Modal>
    </>
  );
}

export function DepartmentsAdminPage({ api, locale }: AdminPageCommonProps) {
  const text = useCopy(locale);
  const { keyword, deferredKeyword, setKeyword } = useKeyword();
  const [items, setItems] = useState<DepartmentListItem[]>([]);
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [code, setCode] = useState("");

  const load = async () => {
    const result = await api.listDepartments({ pageIndex: 1, pageSize: 20, keyword: deferredKeyword });
    setItems(result.items);
  };

  useEffect(() => {
    void load();
  }, [deferredKeyword]);

  return (
    <>
      <PageShell
        title={text.departmentsTitle}
        subtitle={text.departmentsSubtitle}
        testId="app-departments-page"
        toolbar={(
          <SearchToolbar
            value={keyword}
            onChange={setKeyword}
            onRefresh={() => void load()}
            onCreate={() => setOpen(true)}
            placeholder={text.searchPlaceholder}
            createLabel={text.create}
            refreshLabel={text.refresh}
            createTestId="app-departments-create"
          />
        )}
      >
        <Button theme="borderless" {...({ ["data-testid"]: "app-departments-toggle-expand" })}>Expand</Button>
        <DataTable
          testId="app-departments-table"
          emptyText={text.empty}
          items={items}
          columns={[
            { key: "name", title: text.name, render: item => item.name },
            { key: "code", title: text.code, render: item => item.code },
            { key: "actions", title: text.action, render: item => <Button theme="borderless" icon={<IconDelete />} {...({ ["data-testid"]: `app-departments-delete-${item.id}` })} onClick={() => void api.deleteDepartment(item.id).then(load)} /> }
          ]}
        />
      </PageShell>

      <Modal title={text.create} visible={open} onCancel={() => setOpen(false)} onOk={() => void api.createDepartment({ name: name.trim(), code: code.trim(), sortOrder: 10 }).then(() => { setOpen(false); setName(""); setCode(""); return load(); })} okText={text.save} cancelText={text.cancel}>
        <Input value={name} onChange={setName} data-testid="app-departments-form-name" className="module-admin__field" />
        <Input value={code} onChange={setCode} data-testid="app-departments-form-code" className="module-admin__field" />
      </Modal>
    </>
  );
}

export function PositionsAdminPage({ api, locale }: AdminPageCommonProps) {
  const text = useCopy(locale);
  const { keyword, deferredKeyword, setKeyword } = useKeyword();
  const [items, setItems] = useState<PositionListItem[]>([]);
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [code, setCode] = useState("");

  const load = async () => {
    const result = await api.listPositions({ pageIndex: 1, pageSize: 20, keyword: deferredKeyword });
    setItems(result.items);
  };

  useEffect(() => {
    void load();
  }, [deferredKeyword]);

  const submit = async () => {
    const request: PositionCreateRequest = { name: name.trim(), code: code.trim(), isActive: true, sortOrder: 10 };
    await api.createPosition(request);
    setOpen(false);
    setName("");
    setCode("");
    await load();
  };

  return (
    <>
      <PageShell
        title={text.positionsTitle}
        subtitle={text.positionsSubtitle}
        testId="app-positions-page"
        toolbar={(
          <SearchToolbar
            value={keyword}
            onChange={setKeyword}
            onRefresh={() => void load()}
            onCreate={() => setOpen(true)}
            placeholder={text.searchPlaceholder}
            createLabel={text.create}
            refreshLabel={text.refresh}
            createTestId="app-positions-create"
          />
        )}
      >
        <DataTable
          testId="app-positions-table"
          emptyText={text.empty}
          items={items}
          columns={[
            { key: "name", title: text.name, render: item => item.name },
            { key: "code", title: text.code, render: item => item.code },
            { key: "actions", title: text.action, render: item => <Button theme="borderless" icon={<IconDelete />} {...({ ["data-testid"]: `app-positions-delete-${item.id}` })} onClick={() => void api.deletePosition(item.id).then(load)} /> }
          ]}
        />
      </PageShell>

      <Modal title={text.create} visible={open} onCancel={() => setOpen(false)} onOk={() => void submit()} okText={text.save} cancelText={text.cancel}>
        <Input value={name} onChange={setName} data-testid="app-positions-form-name" className="module-admin__field" />
        <Input value={code} onChange={setCode} data-testid="app-positions-form-code" className="module-admin__field" />
      </Modal>
    </>
  );
}

export function ApprovalAdminPage({ api, locale }: AdminPageCommonProps) {
  const text = useCopy(locale);
  const [tab, setTab] = useState("pending");
  const [pending, setPending] = useState<Array<{ id: string; title: string; flowName: string; createdAt: string }>>([]);
  const [done, setDone] = useState<Array<{ id: string; title: string; flowName: string; createdAt: string }>>([]);
  const [requests, setRequests] = useState<Array<{ id: string; title: string; flowName: string; createdAt: string }>>([]);
  const [copies, setCopies] = useState<Array<{ id: string; title: string; flowName: string; createdAt: string }>>([]);

  useEffect(() => {
    const load = async () => {
      if (tab === "pending") {
        const result = await api.listPendingApprovals({ pageIndex: 1, pageSize: 20 });
        setPending(result.items);
        return;
      }

      if (tab === "done") {
        const result = await api.listDoneApprovals({ pageIndex: 1, pageSize: 20 });
        setDone(result.items);
        return;
      }

      if (tab === "requests") {
        const result = await api.listMyRequests({ pageIndex: 1, pageSize: 20 });
        setRequests(result.items);
        return;
      }

      const result = await api.listCopyApprovals({ pageIndex: 1, pageSize: 20 });
      setCopies(result.items);
    };

    void load();
  }, [api, tab]);

  const columns: Column<{ id: string; title: string; flowName: string; createdAt: string }>[] = [
    { key: "title", title: text.name, render: item => item.title },
    { key: "flowName", title: "Flow", render: item => item.flowName },
    { key: "createdAt", title: "Created", render: item => item.createdAt }
  ];

  return (
    <PageShell title="Approval" subtitle="应用内审批工作台" testId="app-approval-page">
      <div className="module-admin__tabs">
        <Button theme={tab === "pending" ? "solid" : "borderless"} onClick={() => setTab("pending")}>{locale === "zh-CN" ? "待办" : "Pending"}</Button>
        <Button theme={tab === "done" ? "solid" : "borderless"} onClick={() => setTab("done")}>{locale === "zh-CN" ? "已办" : "Done"}</Button>
        <Button theme={tab === "requests" ? "solid" : "borderless"} onClick={() => setTab("requests")}>{locale === "zh-CN" ? "我发起" : "My Requests"}</Button>
        <Button theme={tab === "copies" ? "solid" : "borderless"} onClick={() => setTab("copies")}>{locale === "zh-CN" ? "抄送我" : "CC"}</Button>
      </div>
      {tab === "pending" ? <DataTable testId="app-approval-pending-table" emptyText={text.empty} items={pending} columns={columns} /> : null}
      {tab === "done" ? <DataTable testId="app-approval-done-table" emptyText={text.empty} items={done} columns={columns} /> : null}
      {tab === "requests" ? <DataTable testId="app-approval-requests-table" emptyText={text.empty} items={requests} columns={columns} /> : null}
      {tab === "copies" ? <DataTable testId="app-approval-cc-table" emptyText={text.empty} items={copies} columns={columns} /> : null}
    </PageShell>
  );
}

export function ReportsAdminPage({ api }: AdminPageCommonProps) {
  const [items, setItems] = useState<Array<{ id: string; name: string; createdAt: string }>>([]);
  const [open, setOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [name, setName] = useState("");

  const load = async () => {
    const result = await api.listReports({ pageIndex: 1, pageSize: 20 });
    setItems(result.items);
  };

  useEffect(() => {
    void load();
  }, []);

  const submit = async () => {
    if (editingId) {
      await api.updateReport(editingId, { name: name.trim(), configJson: "{}" });
    } else {
      await api.createReport({ name: name.trim(), configJson: "{}" });
    }
    setOpen(false);
    setEditingId(null);
    setName("");
    await load();
  };

  return (
    <>
      <PageShell title="Reports" subtitle="应用报表清单" testId="app-reports-page" toolbar={<Button icon={<IconPlus />} type="primary" {...({ ["data-testid"]: "app-reports-create" })} onClick={() => setOpen(true)}>Create</Button>}>
        <DataTable
          testId="app-reports-table"
          emptyText="No data"
          items={items}
          columns={[
            { key: "name", title: "Name", render: item => item.name },
            { key: "createdAt", title: "Created", render: item => item.createdAt },
            {
              key: "actions",
              title: "Action",
              render: item => (
                <div className="module-admin__actions">
                  <Button theme="borderless" icon={<IconEdit />} {...({ ["data-testid"]: `app-reports-edit-${item.id}` })} onClick={() => { setEditingId(item.id); setName(item.name); setOpen(true); }} />
                  <Button theme="borderless" icon={<IconDelete />} {...({ ["data-testid"]: `app-reports-delete-${item.id}` })} onClick={() => void api.deleteReport(item.id).then(load)} />
                </div>
              )
            }
          ]}
        />
      </PageShell>
      <Modal visible={open} onCancel={() => setOpen(false)} onOk={() => void submit()}>
        <Input value={name} onChange={setName} data-testid="app-reports-form-name" />
      </Modal>
    </>
  );
}

export function DashboardsAdminPage({ api }: AdminPageCommonProps) {
  const [items, setItems] = useState<Array<{ id: string; name: string; createdAt: string }>>([]);
  const [open, setOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [name, setName] = useState("");

  const load = async () => {
    const result = await api.listDashboards({ pageIndex: 1, pageSize: 20 });
    setItems(result.items);
  };

  useEffect(() => {
    void load();
  }, []);

  const submit = async () => {
    if (editingId) {
      await api.updateDashboard(editingId, { name: name.trim(), layoutJson: "{}", isLargeScreen: false });
    } else {
      await api.createDashboard({ name: name.trim(), layoutJson: "{}", isLargeScreen: false });
    }
    setOpen(false);
    setEditingId(null);
    setName("");
    await load();
  };

  return (
    <>
      <PageShell title="Dashboards" subtitle="应用仪表盘清单" testId="app-dashboards-page" toolbar={<Button icon={<IconPlus />} type="primary" {...({ ["data-testid"]: "app-dashboards-create" })} onClick={() => setOpen(true)}>Create</Button>}>
        <DataTable
          testId="app-dashboards-table"
          emptyText="No data"
          items={items}
          columns={[
            { key: "name", title: "Name", render: item => item.name },
            { key: "createdAt", title: "Created", render: item => item.createdAt },
            {
              key: "actions",
              title: "Action",
              render: item => (
                <div className="module-admin__actions">
                  <Button theme="borderless" icon={<IconEdit />} {...({ ["data-testid"]: `app-dashboards-edit-${item.id}` })} onClick={() => { setEditingId(item.id); setName(item.name); setOpen(true); }} />
                  <Button theme="borderless" icon={<IconDelete />} {...({ ["data-testid"]: `app-dashboards-delete-${item.id}` })} onClick={() => void api.deleteDashboard(item.id).then(load)} />
                </div>
              )
            }
          ]}
        />
      </PageShell>
      <Modal visible={open} onCancel={() => setOpen(false)} onOk={() => void submit()}>
        <Input value={name} onChange={setName} data-testid="app-dashboards-form-name" />
      </Modal>
    </>
  );
}

export function VisualizationAdminPage({ api }: AdminPageCommonProps) {
  const [items, setItems] = useState<Array<{ id: string; flowName: string; currentNode?: string | null; startedAt: string }>>([]);

  useEffect(() => {
    void api.listVisualization({ pageIndex: 1, pageSize: 20 }).then(result => setItems(result.items));
  }, [api]);

  return (
    <PageShell title="Visualization" subtitle="流程运行监控" testId="app-visualization-page">
      <DataTable
        testId="app-visualization-table"
        emptyText="No data"
        items={items}
        columns={[
          { key: "flowName", title: "Flow", render: item => item.flowName },
          { key: "currentNode", title: "Node", render: item => item.currentNode || "-" },
          { key: "startedAt", title: "Started", render: item => item.startedAt }
        ]}
      />
    </PageShell>
  );
}

const settingsCopy: Record<AdminLocale, {
  title: string;
  subtitle: string;
  databaseTab: string;
  testConnection: string;
  backupNow: string;
  fileColumn: string;
  createdColumn: string;
  noData: string;
}> = {
  "zh-CN": {
    title: "系统设置",
    subtitle: "数据库运维与基础设置",
    databaseTab: "数据库运维",
    testConnection: "测试连接",
    backupNow: "立即备份",
    fileColumn: "文件名",
    createdColumn: "创建时间",
    noData: "暂无数据"
  },
  "en-US": {
    title: "Settings",
    subtitle: "Database operations and general settings",
    databaseTab: "Database",
    testConnection: "Test Connection",
    backupNow: "Backup Now",
    fileColumn: "File",
    createdColumn: "Created",
    noData: "No data"
  }
};

export function SettingsAdminPage({ api, locale }: AdminPageCommonProps) {
  const [connectionResult, setConnectionResult] = useState<string>("");
  const [backups, setBackups] = useState<Array<{ fileName: string; sizeBytes: number; createdAt: string }>>([]);
  const sc = settingsCopy[locale];

  const load = async () => {
    const result = await api.listBackups();
    setBackups(result);
  };

  useEffect(() => {
    void load();
  }, []);

  return (
    <PageShell title={sc.title} subtitle={sc.subtitle} testId="app-settings-page">
      <div className="module-admin__tabs">
        <Button theme="solid">{sc.databaseTab}</Button>
      </div>
      <div className="module-admin__stack" data-testid="app-settings-db-tab">
        <div className="module-admin__actions">
          <Button {...({ ["data-testid"]: "app-settings-db-test-connection" })} onClick={() => void api.testConnection().then(result => setConnectionResult(result.message))}>{sc.testConnection}</Button>
          <Button {...({ ["data-testid"]: "app-settings-db-backup-now" })} onClick={() => void api.backupNow().then(load)}>{sc.backupNow}</Button>
        </div>
        {connectionResult ? <Banner data-testid="app-settings-db-connection-result" description={connectionResult} /> : null}
        <DataTable testId="app-settings-db-backup-table" emptyText={sc.noData} items={backups} columns={[
          { key: "fileName", title: sc.fileColumn, render: item => item.fileName },
          { key: "createdAt", title: sc.createdColumn, render: item => item.createdAt }
        ]} />
      </div>
    </PageShell>
  );
}

const profileCopy: Record<AdminLocale, {
  title: string;
  subtitle: string;
  displayNameLabel: string;
  displayNamePlaceholder: string;
  emailLabel: string;
  emailPlaceholder: string;
  phoneLabel: string;
  phonePlaceholder: string;
  save: string;
  saveSuccess: string;
  saveFailed: string;
  changePasswordSection: string;
  currentPassword: string;
  currentPasswordPlaceholder: string;
  newPassword: string;
  newPasswordPlaceholder: string;
  confirmPassword: string;
  confirmPasswordPlaceholder: string;
  changePassword: string;
  changePasswordSuccess: string;
  changePasswordFailed: string;
}> = {
  "zh-CN": {
    title: "个人中心",
    subtitle: "查看和编辑当前账号资料。",
    displayNameLabel: "显示名称",
    displayNamePlaceholder: "请输入显示名称",
    emailLabel: "邮箱",
    emailPlaceholder: "请输入邮箱地址",
    phoneLabel: "手机号",
    phonePlaceholder: "请输入手机号",
    save: "保存",
    saveSuccess: "资料已保存。",
    saveFailed: "保存失败，请重试。",
    changePasswordSection: "修改密码",
    currentPassword: "当前密码",
    currentPasswordPlaceholder: "请输入当前密码",
    newPassword: "新密码",
    newPasswordPlaceholder: "请输入新密码（至少8位，含大小写字母、数字、特殊字符）",
    confirmPassword: "确认新密码",
    confirmPasswordPlaceholder: "请再次输入新密码",
    changePassword: "修改密码",
    changePasswordSuccess: "密码已修改成功。",
    changePasswordFailed: "密码修改失败，请检查当前密码是否正确。"
  },
  "en-US": {
    title: "Profile",
    subtitle: "View and edit the current account profile.",
    displayNameLabel: "Display Name",
    displayNamePlaceholder: "Enter your display name",
    emailLabel: "Email",
    emailPlaceholder: "Enter your email address",
    phoneLabel: "Phone Number",
    phonePlaceholder: "Enter your phone number",
    save: "Save",
    saveSuccess: "Profile saved.",
    saveFailed: "Failed to save profile. Please try again.",
    changePasswordSection: "Change Password",
    currentPassword: "Current Password",
    currentPasswordPlaceholder: "Enter current password",
    newPassword: "New Password",
    newPasswordPlaceholder: "Enter new password (min. 8 chars, upper/lowercase, digit, special char)",
    confirmPassword: "Confirm New Password",
    confirmPasswordPlaceholder: "Re-enter your new password",
    changePassword: "Change Password",
    changePasswordSuccess: "Password changed successfully.",
    changePasswordFailed: "Failed to change password. Please check your current password."
  }
};

export function ProfileAdminPage({ api, locale }: AdminPageCommonProps) {
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const pc = profileCopy[locale];

  useEffect(() => {
    void api.getProfile().then(profile => {
      setDisplayName(profile.displayName);
      setEmail(profile.email ?? "");
      setPhoneNumber(profile.phoneNumber ?? "");
    });
  }, [api]);

  const handleSave = () => {
    void api.updateProfile({ displayName, email: email || undefined, phoneNumber: phoneNumber || undefined })
      .then(() => Toast.success(pc.saveSuccess))
      .catch(() => Toast.error(pc.saveFailed));
  };

  const handleChangePassword = () => {
    void api.changePassword({ currentPassword, newPassword, confirmPassword })
      .then(() => {
        Toast.success(pc.changePasswordSuccess);
        setCurrentPassword("");
        setNewPassword("");
        setConfirmPassword("");
      })
      .catch(() => Toast.error(pc.changePasswordFailed));
  };

  return (
    <PageShell title={pc.title} subtitle={pc.subtitle} testId="app-profile-page">
      <div className="module-admin__stack">
        <div className="module-admin__field">
          <label className="module-admin__label">{pc.displayNameLabel}</label>
          <Input value={displayName} onChange={setDisplayName} placeholder={pc.displayNamePlaceholder} />
        </div>
        <div className="module-admin__field">
          <label className="module-admin__label">{pc.emailLabel}</label>
          <Input value={email} onChange={setEmail} placeholder={pc.emailPlaceholder} />
        </div>
        <div className="module-admin__field">
          <label className="module-admin__label">{pc.phoneLabel}</label>
          <Input value={phoneNumber} onChange={setPhoneNumber} placeholder={pc.phonePlaceholder} />
        </div>
        <Button theme="solid" onClick={handleSave}>{pc.save}</Button>
      </div>
      <div className="module-admin__stack module-admin__stack--section">
        <Typography.Title heading={5} style={{ margin: "16px 0 8px" }}>{pc.changePasswordSection}</Typography.Title>
        <div className="module-admin__field">
          <label className="module-admin__label">{pc.currentPassword}</label>
          <Input mode="password" value={currentPassword} onChange={setCurrentPassword} placeholder={pc.currentPasswordPlaceholder} />
        </div>
        <div className="module-admin__field">
          <label className="module-admin__label">{pc.newPassword}</label>
          <Input mode="password" value={newPassword} onChange={setNewPassword} placeholder={pc.newPasswordPlaceholder} />
        </div>
        <div className="module-admin__field">
          <label className="module-admin__label">{pc.confirmPassword}</label>
          <Input mode="password" value={confirmPassword} onChange={setConfirmPassword} placeholder={pc.confirmPasswordPlaceholder} />
        </div>
        <Button onClick={handleChangePassword}>{pc.changePassword}</Button>
      </div>
    </PageShell>
  );
}
