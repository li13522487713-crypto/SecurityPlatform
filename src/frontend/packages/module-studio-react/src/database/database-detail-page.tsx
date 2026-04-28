import { useEffect, useMemo, useRef, useState } from "react";
import {
  Banner,
  Button,
  Checkbox,
  Dropdown,
  Empty,
  Form,
  Input,
  Modal,
  Select,
  Space,
  Switch,
  Table,
  Tabs,
  Tag,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { IconArrowLeft, IconChevronDown, IconEditStroked, IconList, IconPlus } from "@douyinfe/semi-icons";
import { getStudioCopy, formatStudioTemplate } from "../copy";
import { ResourceReferenceCard } from "../shared/resource-reference-card";
import type {
  StudioDatabaseChannelConfigItem,
  StudioDatabaseDetail,
  StudioDatabaseFieldItem,
  StudioDatabaseRecordItem,
  StudioDatabaseRecordUpsertRequest,
  StudioPageProps
} from "../types";

type DatabaseTabKey = "structure" | "draft" | "online";

const FIELD_TYPE_OPTIONS = [
  { labelKey: "typeString" as const, value: "string" },
  { labelKey: "typeNumber" as const, value: "number" },
  { labelKey: "typeInteger" as const, value: "integer" },
  { labelKey: "typeBoolean" as const, value: "boolean" },
  { labelKey: "typeDate" as const, value: "date" },
  { labelKey: "typeJson" as const, value: "json" },
  { labelKey: "typeArray" as const, value: "array" }
];

function formatDate(value?: string): string {
  if (!value) {
    return "-";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return date.toLocaleString();
}

function tryFormatJson(value?: string): string {
  if (!value) {
    return "{}";
  }
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function parseSchemaFields(tableSchema?: string): StudioDatabaseFieldItem[] {
  if (!tableSchema) {
    return [];
  }
  try {
    const parsed = JSON.parse(tableSchema) as Array<Record<string, unknown>>;
    return parsed
      .map((item, index) => ({
        name: String(item.name ?? ""),
        description: item.description ? String(item.description) : undefined,
        type: String(item.type ?? "string"),
        required: Boolean(item.required),
        indexed: Boolean(item.indexed),
        sortOrder: index,
        isSystemField: false
      }))
      .filter(item => item.name.trim());
  } catch {
    return [];
  }
}

function getFieldList(detail: StudioDatabaseDetail | null): StudioDatabaseFieldItem[] {
  if (!detail) {
    return [];
  }
  if (detail.fields && detail.fields.length > 0) {
    return [...detail.fields].sort((left, right) => (left.sortOrder ?? 0) - (right.sortOrder ?? 0));
  }
  return parseSchemaFields(detail.tableSchema);
}

function inferChannelConfig(detail: StudioDatabaseDetail | null): StudioDatabaseChannelConfigItem[] {
  return detail?.channelConfigs ?? [];
}

function environmentOf(tab: DatabaseTabKey): number {
  return tab === "online" ? 2 : 1;
}

function buildDatabaseRequest(detail: StudioDatabaseDetail, fields: StudioDatabaseFieldItem[]) {
  return {
    name: detail.name,
    description: detail.description,
    botId: detail.botId,
    workspaceId: detail.workspaceId,
    queryMode: detail.queryMode,
    channelScope: detail.channelScope,
    fields: fields.map((field, index) => ({
      ...field,
      sortOrder: index
    }))
  };
}

export function DatabaseDetailPageImpl({
  api,
  locale,
  databaseId,
  onOpenLibrary,
  onNavigateBack
}: StudioPageProps & {
  databaseId: string;
  onOpenLibrary: () => void;
  onNavigateBack?: () => void;
}) {
  const c = getStudioCopy(locale);
  const db = c.databaseDetail;

  const [detail, setDetail] = useState<StudioDatabaseDetail | null>(null);
  const [activeTab, setActiveTab] = useState<DatabaseTabKey>("structure");
  const [records, setRecords] = useState<StudioDatabaseRecordItem[]>([]);
  const [recordsLoading, setRecordsLoading] = useState(false);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize] = useState(10);
  const [total, setTotal] = useState(0);
  const [structureModalVisible, setStructureModalVisible] = useState(false);
  const [fieldDrafts, setFieldDrafts] = useState<StudioDatabaseFieldItem[]>([]);
  const [savingStructure, setSavingStructure] = useState(false);
  const [renameOpen, setRenameOpen] = useState(false);
  const [nameDraft, setNameDraft] = useState("");
  const [savingName, setSavingName] = useState(false);
  const [recordDialogVisible, setRecordDialogVisible] = useState(false);
  const [recordDraft, setRecordDraft] = useState("{\n  \n}");
  const [editingRecordId, setEditingRecordId] = useState<number | null>(null);
  const [recordSaving, setRecordSaving] = useState(false);
  const [channelConfigVisible, setChannelConfigVisible] = useState(false);
  const [channelConfigDrafts, setChannelConfigDrafts] = useState<StudioDatabaseChannelConfigItem[]>([]);
  const [savingConfig, setSavingConfig] = useState(false);
  const [modeSaving, setModeSaving] = useState(false);
  const importInputRef = useRef<HTMLInputElement | null>(null);

  const fieldList = useMemo(() => getFieldList(detail), [detail]);
  const isRecordTab = activeTab === "draft" || activeTab === "online";

  const fieldTypeLabel = (type?: string) => {
    const t = (type ?? "string").toLowerCase();
    const map: Record<string, string> = {
      string: db.typeString,
      number: db.typeNumber,
      integer: db.typeInteger,
      boolean: db.typeBoolean,
      date: db.typeDate,
      json: db.typeJson,
      array: db.typeArray
    };
    return map[t] ?? db.typeUnknown;
  };

  const isolationLabel = (scope?: number) => {
    switch (scope ?? 0) {
      case 1:
        return db.isolationChannel;
      case 2:
        return db.isolationInternal;
      default:
        return db.isolationFullShared;
    }
  };

  const userModeLabel = (mode?: number) =>
    (mode ?? 0) === 1 ? db.userModeSingle : db.userModeMulti;

  async function loadDetail() {
    try {
      const nextDetail = await api.getDatabaseDetail(databaseId);
      setDetail(nextDetail);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : db.toastLoadDetailFailed);
    }
  }

  async function loadRecords(nextPageIndex = pageIndex, nextTab = activeTab) {
    if (nextTab === "structure") {
      return;
    }
    setRecordsLoading(true);
    try {
      const result = await api.listDatabaseRecords(databaseId, {
        pageIndex: nextPageIndex,
        pageSize,
        environment: environmentOf(nextTab)
      });
      setRecords(result.items);
      setTotal(result.total);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : db.toastLoadRecordsFailed);
    } finally {
      setRecordsLoading(false);
    }
  }

  useEffect(() => {
    void loadDetail();
  }, [databaseId]);

  useEffect(() => {
    if (isRecordTab) {
      void loadRecords(pageIndex, activeTab);
    }
  }, [databaseId, activeTab, pageIndex, isRecordTab]);

  const recordColumns = useMemo<ColumnProps<StudioDatabaseRecordItem>[]>(() => {
    const dataFields = fieldList.filter(field => !field.isSystemField);
    const columns: ColumnProps<StudioDatabaseRecordItem>[] = dataFields.map(field => ({
      title: field.name,
      dataIndex: field.name,
      render: (_value, record) => {
        try {
          const parsed = JSON.parse(record.dataJson) as Record<string, unknown>;
          const value = parsed[field.name];
          return typeof value === "string" ? value : JSON.stringify(value ?? "");
        } catch {
          return "-";
        }
      }
    }));

    columns.push(
      {
        title: db.colChannel,
        dataIndex: "channelId",
        render: (_value, record) => record.channelId || "-"
      },
      {
        title: db.colUser,
        dataIndex: "ownerUserId",
        render: (_value, record) => record.ownerUserId ?? "-"
      },
      {
        title: db.colCreatedAt,
        dataIndex: "createdAt",
        render: (_value, record) => formatDate(record.updatedAt || record.createdAt)
      },
      {
        title: db.actions,
        dataIndex: "id",
        render: (_value, record) => (
          <Space>
            <Button size="small" onClick={() => openEditRecord(record)}>
              {db.edit}
            </Button>
            <Button size="small" type="danger" theme="borderless" onClick={() => void handleDeleteRecord(record.id)}>
              {db.delete}
            </Button>
          </Space>
        )
      }
    );

    return columns;
  }, [fieldList, db]);

  function goBack() {
    (onNavigateBack ?? onOpenLibrary)();
  }

  function openStructureModal() {
    setFieldDrafts(fieldList.filter(field => !field.isSystemField).map(field => ({ ...field })));
    setStructureModalVisible(true);
  }

  function openRenameModal() {
    if (!detail) {
      return;
    }
    setNameDraft(detail.name);
    setRenameOpen(true);
  }

  function openChannelConfigModal() {
    setChannelConfigDrafts(inferChannelConfig(detail).map(item => ({ ...item })));
    setChannelConfigVisible(true);
  }

  async function applyChannelScope(nextScope: number) {
    if (!detail || !api.updateDatabaseMode) {
      Toast.error(db.toastModeNotAvailable);
      return;
    }
    setModeSaving(true);
    try {
      await api.updateDatabaseMode(databaseId, {
        queryMode: detail.queryMode ?? 0,
        channelScope: nextScope
      });
      await loadDetail();
      Toast.success(db.toastModeSaved);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : db.toastModeSaveFailed);
    } finally {
      setModeSaving(false);
    }
  }

  async function applyQueryMode(nextMode: number) {
    if (!detail || !api.updateDatabaseMode) {
      Toast.error(db.toastModeNotAvailable);
      return;
    }
    setModeSaving(true);
    try {
      await api.updateDatabaseMode(databaseId, {
        queryMode: nextMode,
        channelScope: detail.channelScope ?? 0
      });
      await loadDetail();
      Toast.success(db.toastModeSaved);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : db.toastModeSaveFailed);
    } finally {
      setModeSaving(false);
    }
  }

  async function handleSaveRename() {
    if (!detail || !api.updateDatabase) {
      Toast.error(db.toastSchemaNotEditable);
      return;
    }
    const trimmed = nameDraft.trim();
    if (!trimmed) {
      return;
    }
    setSavingName(true);
    try {
      await api.updateDatabase(databaseId, {
        ...buildDatabaseRequest(detail, fieldList),
        name: trimmed
      });
      setRenameOpen(false);
      await loadDetail();
      Toast.success(db.toastStructureSaved);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : db.toastStructureSaveFailed);
    } finally {
      setSavingName(false);
    }
  }

  function openCreateRecord() {
    setEditingRecordId(null);
    setRecordDraft("{\n  \n}");
    setRecordDialogVisible(true);
  }

  function openEditRecord(record: StudioDatabaseRecordItem) {
    setEditingRecordId(record.id);
    setRecordDraft(tryFormatJson(record.dataJson));
    setRecordDialogVisible(true);
  }

  async function handleSaveStructure() {
    if (!detail || !api.updateDatabase) {
      Toast.error(db.toastSchemaNotEditable);
      return;
    }
    const normalizedFields = fieldDrafts
      .map((field, index) => ({
        ...field,
        name: field.name.trim(),
        type: field.type || "string",
        sortOrder: index
      }))
      .filter(field => field.name);
    if (normalizedFields.length === 0) {
      Toast.error(db.toastMinOneField);
      return;
    }
    setSavingStructure(true);
    try {
      await api.updateDatabase(
        databaseId,
        buildDatabaseRequest(detail, [...fieldList.filter(field => field.isSystemField), ...normalizedFields])
      );
      setStructureModalVisible(false);
      await loadDetail();
      Toast.success(db.toastStructureSaved);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : db.toastStructureSaveFailed);
    } finally {
      setSavingStructure(false);
    }
  }

  async function handleSaveRecord() {
    let normalizedJson = "{}";
    try {
      const parsed = JSON.parse(recordDraft);
      normalizedJson = JSON.stringify(parsed);
    } catch {
      Toast.error(db.toastJsonInvalid);
      return;
    }
    const payload: StudioDatabaseRecordUpsertRequest = {
      dataJson: normalizedJson,
      environment: environmentOf(activeTab)
    };
    setRecordSaving(true);
    try {
      if (editingRecordId) {
        await api.updateDatabaseRecord(databaseId, editingRecordId, payload);
      } else {
        await api.createDatabaseRecord(databaseId, payload);
      }
      setRecordDialogVisible(false);
      await Promise.all([loadDetail(), loadRecords()]);
      Toast.success(editingRecordId ? db.toastRecordUpdated : db.toastRecordSaved);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : db.toastRecordSaveFailed);
    } finally {
      setRecordSaving(false);
    }
  }

  async function handleDeleteRecord(recordId: number) {
    try {
      await api.deleteDatabaseRecord(databaseId, recordId, environmentOf(activeTab));
      await Promise.all([loadDetail(), loadRecords()]);
      Toast.success(db.toastRecordDeleted);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : db.toastRecordDeleteFailed);
    }
  }

  async function handleSaveChannelConfig() {
    if (!api.updateDatabaseChannelConfigs) {
      Toast.error(db.toastChannelRwNotAvailable);
      return;
    }
    setSavingConfig(true);
    try {
      await api.updateDatabaseChannelConfigs(databaseId, { items: channelConfigDrafts });
      setChannelConfigVisible(false);
      await loadDetail();
      Toast.success(db.toastChannelRwSaved);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : db.toastChannelRwSaveFailed);
    } finally {
      setSavingConfig(false);
    }
  }

  async function handleImportFile(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) {
      return;
    }
    try {
      await api.submitDatabaseImport(databaseId, file, environmentOf(activeTab));
      Toast.success(db.toastImportSubmitted);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : db.toastImportFailed);
    }
  }

  const isolationMenu = (
    <div
      style={{
        padding: "12px 16px",
        minWidth: 320,
        background: "var(--semi-color-bg-2)",
        borderRadius: 8,
        boxShadow: "var(--semi-shadow-elevated)"
      }}
    >
      <Typography.Title heading={6} style={{ marginBottom: 8 }}>
        {db.popoverIsolationTitle}
      </Typography.Title>
      {[
        { scope: 1, title: db.isolationChannel, desc: db.isolationChannelDesc },
        { scope: 2, title: db.isolationInternal, desc: db.isolationInternalDesc },
        { scope: 0, title: db.isolationFullShared, desc: db.isolationFullSharedDesc }
      ].map(row => (
        <div
          key={row.scope}
          role="button"
          tabIndex={0}
          onClick={() => {
            if (!modeSaving) {
              void applyChannelScope(row.scope);
            }
          }}
          onKeyDown={e => {
            if (e.key === "Enter" || e.key === " ") {
              e.preventDefault();
              if (!modeSaving) {
                void applyChannelScope(row.scope);
              }
            }
          }}
          style={{
            padding: "10px 0",
            cursor: modeSaving ? "wait" : "pointer",
            borderBottom: "1px solid var(--semi-color-border)"
          }}
        >
          <div style={{ fontWeight: 600 }}>{row.title}</div>
          <Typography.Text type="tertiary" size="small">
            {row.desc}
          </Typography.Text>
        </div>
      ))}
    </div>
  );

  const userModeMenu = (
    <div
      style={{
        padding: "12px 16px",
        minWidth: 320,
        background: "var(--semi-color-bg-2)",
        borderRadius: 8,
        boxShadow: "var(--semi-shadow-elevated)"
      }}
    >
      <Typography.Title heading={6} style={{ marginBottom: 8 }}>
        {db.popoverUserModeTitle}
      </Typography.Title>
      {[
        { mode: 1, title: db.userModeSingle, desc: db.userModeSingleDesc },
        { mode: 0, title: db.userModeMulti, desc: db.userModeMultiDesc }
      ].map(row => (
        <div
          key={row.mode}
          role="button"
          tabIndex={0}
          onClick={() => {
            if (!modeSaving) {
              void applyQueryMode(row.mode);
            }
          }}
          onKeyDown={e => {
            if (e.key === "Enter" || e.key === " ") {
              e.preventDefault();
              if (!modeSaving) {
                void applyQueryMode(row.mode);
              }
            }
          }}
          style={{
            padding: "10px 0",
            cursor: modeSaving ? "wait" : "pointer",
            borderBottom: "1px solid var(--semi-color-border)"
          }}
        >
          <div style={{ fontWeight: 600 }}>{row.title}</div>
          <Typography.Text type="tertiary" size="small">
            {row.desc}
          </Typography.Text>
        </div>
      ))}
    </div>
  );

  const fieldSelectOptions = FIELD_TYPE_OPTIONS.map(o => ({
    value: o.value,
    label: db[o.labelKey]
  }));

  return (
    <section className="module-studio__page coze-database-detail" data-testid="app-studio-database-detail-page-v2">
      {detail ? (
        <div className="module-studio__header" style={{ alignItems: "flex-start", flexWrap: "wrap", gap: 12 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 12, flex: "1 1 280px", minWidth: 0 }}>
            <Button icon={<IconArrowLeft />} theme="borderless" type="tertiary" onClick={goBack} aria-label={db.backToLibrary} />
            <div
              style={{
                width: 40,
                height: 40,
                borderRadius: 10,
                background: "var(--semi-color-fill-0)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                color: "var(--semi-color-primary)"
              }}
            >
              <IconList size="large" />
            </div>
            <div style={{ minWidth: 0 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 6, flexWrap: "wrap" }}>
                <Typography.Title heading={4} style={{ margin: 0 }}>
                  {detail.name}
                </Typography.Title>
                <Button icon={<IconEditStroked />} theme="borderless" type="tertiary" size="small" onClick={openRenameModal} />
              </div>
              <Typography.Text type="tertiary" size="small">
                {db.subtitle}
              </Typography.Text>
            </div>
          </div>
          <Space wrap style={{ marginLeft: "auto" }}>
            <Button onClick={openChannelConfigModal}>{db.channelReadWrite}</Button>
            <Dropdown trigger="click" position="bottomRight" render={isolationMenu}>
              <Button theme="light" loading={modeSaving}>
                {db.channelIsolation}：{isolationLabel(detail.channelScope)}
                <IconChevronDown style={{ marginLeft: 4 }} />
              </Button>
            </Dropdown>
            <Dropdown trigger="click" position="bottomRight" render={userModeMenu}>
              <Button theme="light" loading={modeSaving}>
                {userModeLabel(detail.queryMode)}
                <IconChevronDown style={{ marginLeft: 4 }} />
              </Button>
            </Dropdown>
            <Button icon={<IconEditStroked />} onClick={openStructureModal}>
              {db.editStructure}
            </Button>
            <Button onClick={onOpenLibrary}>{db.backToLibrary}</Button>
          </Space>
        </div>
      ) : (
        <div className="module-studio__header">
          <Typography.Title heading={4} style={{ margin: 0 }}>
            {db.notFound}
          </Typography.Title>
        </div>
      )}

      <div className="module-studio__surface">
        {detail ? (
          <div className="module-studio__stack">
            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 8 }}>
              <Tag color="orange" size="large">
                {db.draftCount} {detail.draftRecordCount ?? 0}
              </Tag>
              <Tag color="green" size="large">
                {db.onlineCount} {detail.onlineRecordCount ?? 0}
              </Tag>
            </div>
            <ResourceReferenceCard api={api} locale={locale} resourceType="database" resourceId={String(databaseId)} />
            <Tabs
              activeKey={activeTab}
              onChange={key => {
                setActiveTab(key as DatabaseTabKey);
                setPageIndex(1);
              }}
              type="line"
              tabList={[
                { itemKey: "structure", tab: db.tabStructure },
                { itemKey: "draft", tab: db.tabDraft },
                { itemKey: "online", tab: db.tabOnline }
              ]}
            />

            {activeTab === "structure" ? (
              <div className="module-studio__coze-inspector-card">
                <div className="module-studio__card-head">
                  <strong>{db.tabStructure}</strong>
                  <Typography.Text type="tertiary">
                    {formatStudioTemplate(db.structureHintTemplate, { count: fieldList.length })}
                  </Typography.Text>
                </div>
                <Table
                  rowKey={record => `${record.name}-${record.sortOrder ?? 0}`}
                  pagination={false}
                  dataSource={fieldList}
                  columns={[
                    { title: db.colFieldName, dataIndex: "name" },
                    { title: db.colDescription, dataIndex: "description", render: value => value || "-" },
                    {
                      title: db.colIndexed,
                      dataIndex: "indexed",
                      render: (_v, record) =>
                        record.isSystemField && record.name === "id" ? (
                          <Checkbox checked disabled />
                        ) : record.indexed ? (
                          <Checkbox checked disabled />
                        ) : (
                          "-"
                        )
                    },
                    { title: db.colType, dataIndex: "type", render: (_v, record) => fieldTypeLabel(record.type) },
                    {
                      title: db.colRequired,
                      dataIndex: "required",
                      render: value => (value ? db.yes : db.no)
                    }
                  ]}
                />
              </div>
            ) : (
              <div className="module-studio__coze-inspector-card">
                <div className="module-studio__card-head">
                  <strong>{activeTab === "draft" ? db.tabDraft : db.tabOnline}</strong>
                  <Space>
                    <Button icon={<IconPlus />} theme="solid" type="primary" onClick={openCreateRecord}>
                      {db.addRecord}
                    </Button>
                    <Button onClick={() => void api.downloadDatabaseTemplate(databaseId)}>{db.downloadTemplate}</Button>
                    <Button onClick={() => importInputRef.current?.click()}>{db.importData}</Button>
                  </Space>
                </div>
                <input
                  ref={importInputRef}
                  type="file"
                  accept=".csv,text/csv"
                  style={{ display: "none" }}
                  onChange={event => void handleImportFile(event)}
                />
                {recordsLoading ? (
                  <Typography.Text type="tertiary">{db.loadingRecords}</Typography.Text>
                ) : records.length === 0 ? (
                  <Empty image={null} title={c.common.emptyData} />
                ) : (
                  <Table
                    rowKey="id"
                    columns={recordColumns}
                    dataSource={records}
                    pagination={{
                      currentPage: pageIndex,
                      pageSize,
                      total,
                      onPageChange: page => setPageIndex(page)
                    }}
                  />
                )}
              </div>
            )}
          </div>
        ) : (
          <Empty image={null} title={db.notFound} />
        )}
      </div>

      <Modal
        title={db.modalRenameTitle}
        visible={renameOpen}
        onCancel={() => !savingName && setRenameOpen(false)}
        onOk={() => void handleSaveRename()}
        confirmLoading={savingName}
      >
        <Input value={nameDraft} onChange={setNameDraft} />
      </Modal>

      <Modal
        title={db.modalEditStructure}
        visible={structureModalVisible}
        width={900}
        onCancel={() => !savingStructure && setStructureModalVisible(false)}
        onOk={() => void handleSaveStructure()}
        confirmLoading={savingStructure}
      >
        <div className="module-studio__stack">
          {fieldDrafts.map((field, index) => (
            <div
              key={`${field.name}-${index}`}
              style={{ display: "grid", gridTemplateColumns: "2fr 2fr 1.2fr auto auto", gap: 12 }}
            >
              <Input
                value={field.name}
                placeholder={db.fieldNamePh}
                onChange={value =>
                  setFieldDrafts(current => current.map((item, itemIndex) => (itemIndex === index ? { ...item, name: value } : item)))
                }
              />
              <Input
                value={field.description}
                placeholder={db.fieldDescPh}
                onChange={value =>
                  setFieldDrafts(current => current.map((item, itemIndex) => (itemIndex === index ? { ...item, description: value } : item)))
                }
              />
              <Select
                value={field.type}
                optionList={fieldSelectOptions}
                onChange={value =>
                  setFieldDrafts(current => current.map((item, itemIndex) => (itemIndex === index ? { ...item, type: String(value) } : item)))
                }
              />
              <Switch
                checked={Boolean(field.required)}
                checkedText={db.requiredOn}
                uncheckedText={db.requiredOff}
                onChange={checked =>
                  setFieldDrafts(current => current.map((item, itemIndex) => (itemIndex === index ? { ...item, required: checked } : item)))
                }
              />
              <Button type="danger" theme="borderless" onClick={() => setFieldDrafts(current => current.filter((_, itemIndex) => itemIndex !== index))}>
                {db.removeField}
              </Button>
            </div>
          ))}
          <Button
            icon={<IconPlus />}
            onClick={() =>
              setFieldDrafts(current => [
                ...current,
                { name: "", description: "", type: "string", required: false, indexed: false, isSystemField: false, sortOrder: current.length }
              ])
            }
          >
            {db.addField}
          </Button>
        </div>
      </Modal>

      <Modal
        title={editingRecordId ? formatStudioTemplate(db.modalRecordEditTemplate, { id: editingRecordId }) : db.modalRecordCreate}
        visible={recordDialogVisible}
        width={720}
        onCancel={() => !recordSaving && setRecordDialogVisible(false)}
        onOk={() => void handleSaveRecord()}
        confirmLoading={recordSaving}
      >
        <Typography.Text type="tertiary">
          {formatStudioTemplate(db.writeTargetHintTemplate, {
            target: activeTab === "online" ? db.tabOnline : db.tabDraft
          })}
        </Typography.Text>
        <Form style={{ marginTop: 12 }}>
          <Form.Slot>
            <Input.TextArea rows={16} value={recordDraft} onChange={setRecordDraft} />
          </Form.Slot>
        </Form>
      </Modal>

      <Modal
        title={db.modalChannelRw}
        visible={channelConfigVisible}
        width={820}
        onCancel={() => !savingConfig && setChannelConfigVisible(false)}
        onOk={() => void handleSaveChannelConfig()}
        confirmLoading={savingConfig}
      >
        {channelConfigDrafts.length === 0 ? (
          <Banner type="info" bordered={false} closeIcon={null} description={db.modalChannelRwEmpty} />
        ) : (
          <Table
            rowKey="channelKey"
            pagination={false}
            dataSource={channelConfigDrafts}
            columns={[
              { title: db.colChannelName, dataIndex: "displayName" },
              { title: db.colChannelType, dataIndex: "publishChannelType", render: value => value || "-" },
              {
                title: db.linkDraftData,
                dataIndex: "allowDraft",
                render: (_value, record, index) => (
                  <Typography.Text
                    link
                    style={{ color: record.allowDraft ? "var(--semi-color-primary)" : undefined }}
                    onClick={() =>
                      setChannelConfigDrafts(current =>
                        current.map((item, itemIndex) => (itemIndex === index ? { ...item, allowDraft: !item.allowDraft } : item))
                      )
                    }
                  >
                    {record.allowDraft ? db.yes : db.no}
                  </Typography.Text>
                )
              },
              {
                title: db.linkOnlineData,
                dataIndex: "allowOnline",
                render: (_value, record, index) => (
                  <Typography.Text
                    link
                    style={{ color: record.allowOnline ? "var(--semi-color-primary)" : undefined }}
                    onClick={() =>
                      setChannelConfigDrafts(current =>
                        current.map((item, itemIndex) => (itemIndex === index ? { ...item, allowOnline: !item.allowOnline } : item))
                      )
                    }
                  >
                    {record.allowOnline ? db.yes : db.no}
                  </Typography.Text>
                )
              }
            ]}
          />
        )}
      </Modal>
    </section>
  );
}
