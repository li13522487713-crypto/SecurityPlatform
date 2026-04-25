import { useEffect, useMemo, useState } from "react";
import { Button, Input, Select, SideSheet, Space, Switch, Table, TextArea, Toast, Typography } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { IconPlus, IconRefresh, IconSave } from "@douyinfe/semi-icons";
import {
  createAiDatabaseHostProfile,
  deleteAiDatabaseHostProfile,
  listAiDatabaseHostProfiles,
  testAiDatabaseHostProfile,
  updateAiDatabaseHostProfile,
  type AiDatabaseHostProfile,
  type AiDatabaseHostProfileMutationRequest
} from "../../../services/api-ai-database-host-profiles";
import type { DatabaseCenterLabels } from "./database-center-labels";

const { Text } = Typography;

interface HostProfileManageDrawerProps {
  labels: DatabaseCenterLabels;
  visible: boolean;
  onClose: () => void;
}

const emptyForm: AiDatabaseHostProfileMutationRequest = {
  name: "",
  driverCode: "SQLite",
  description: "",
  connectionString: "",
  sqliteRootPath: "data/ai-db/e2e",
  defaultDatabaseName: "",
  defaultSchemaName: "",
  maxPoolSize: 50,
  connectionTimeoutSeconds: 15,
  isActive: true,
  isDefault: false
};

export function HostProfileManageDrawer({ labels, visible, onClose }: HostProfileManageDrawerProps) {
  const [profiles, setProfiles] = useState<AiDatabaseHostProfile[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [form, setForm] = useState<AiDatabaseHostProfileMutationRequest>(emptyForm);
  const [loading, setLoading] = useState(false);
  const selected = useMemo(() => profiles.find(item => item.id === selectedId) ?? null, [profiles, selectedId]);

  useEffect(() => {
    if (visible) {
      void load();
    }
  }, [visible]);

  useEffect(() => {
    if (!selected) {
      setForm(emptyForm);
      return;
    }

    setForm({
      name: selected.name,
      driverCode: selected.driverCode,
      description: selected.description ?? "",
      connectionString: "",
      sqliteRootPath: selected.sqliteRootPath ?? "data/ai-db/e2e",
      defaultDatabaseName: selected.defaultDatabaseName ?? "",
      defaultSchemaName: selected.defaultSchemaName ?? "",
      maxPoolSize: selected.maxPoolSize ?? 50,
      connectionTimeoutSeconds: selected.connectionTimeoutSeconds ?? 15,
      isActive: selected.isActive,
      isDefault: selected.isDefault
    });
  }, [selected]);

  const columns: ColumnProps<AiDatabaseHostProfile>[] = [
    {
      title: labels.name,
      dataIndex: "name",
      render: (_value: unknown, record) => <Button theme="borderless" onClick={() => setSelectedId(record.id)}>{record.name}</Button>
    },
    { title: labels.driver, dataIndex: "driverCode", width: 120 },
    { title: "Connection", dataIndex: "maskedConnectionSummary", render: (value: unknown) => value ? String(value) : "-" },
    { title: "Status", dataIndex: "status", width: 110, render: (value: unknown) => value ? String(value) : "-" }
  ];

  return (
    <SideSheet visible={visible} onCancel={onClose} title={labels.hostProfiles} width={1080}>
      <div style={{ display: "grid", gridTemplateColumns: "minmax(0, 1fr) 360px", gap: 16 }}>
        <Space vertical align="start" style={{ width: "100%" }}>
          <Space>
            <Button icon={<IconRefresh />} onClick={() => void load()}>{labels.refresh}</Button>
            <Button icon={<IconPlus />} onClick={() => setSelectedId("")}>{labels.createHostProfile}</Button>
          </Space>
          <Table rowKey="id" loading={loading} size="small" columns={columns} dataSource={profiles} pagination={false} />
        </Space>
        <Space vertical align="start" style={{ width: "100%" }}>
          <Text strong>{selected ? labels.editHostProfile : labels.createHostProfile}</Text>
          <Input placeholder={labels.name} value={form.name} onChange={value => setForm({ ...form, name: value })} />
          <Select
            value={form.driverCode}
            onChange={value => setForm({ ...form, driverCode: String(value) })}
            optionList={["SQLite", "MySql", "PostgreSQL", "SqlServer", "Oracle", "Dm", "Kdbndp"].map(value => ({ value, label: value }))}
          />
          <TextArea style={{ width: "100%" }} placeholder={labels.description} value={form.description ?? ""} onChange={(value: string) => setForm({ ...form, description: value })} />
          <TextArea
            style={{ width: "100%" }}
            placeholder={form.driverCode === "SQLite" ? labels.sqliteRootPath : labels.connectionString}
            value={(form.driverCode === "SQLite" ? form.sqliteRootPath : form.connectionString) ?? ""}
            onChange={(value: string) => setForm(form.driverCode === "SQLite" ? { ...form, sqliteRootPath: value } : { ...form, connectionString: value })}
          />
          <Input placeholder={labels.defaultDatabase} value={form.defaultDatabaseName ?? ""} onChange={value => setForm({ ...form, defaultDatabaseName: value })} />
          <Input placeholder={labels.defaultSchema} value={form.defaultSchemaName ?? ""} onChange={value => setForm({ ...form, defaultSchemaName: value })} />
          <Space>
            <Text>{labels.active}</Text>
            <Switch checked={Boolean(form.isActive)} onChange={checked => setForm({ ...form, isActive: checked })} />
            <Text>{labels.defaultProfile}</Text>
            <Switch checked={Boolean(form.isDefault)} onChange={checked => setForm({ ...form, isDefault: checked })} />
          </Space>
          <Space>
            <Button loading={loading} onClick={() => void test()}>{labels.testConnection}</Button>
            <Button icon={<IconSave />} loading={loading} theme="solid" onClick={() => void save()}>{labels.save}</Button>
            {selected ? <Button type="danger" loading={loading} onClick={() => void remove()}>{labels.delete}</Button> : null}
          </Space>
        </Space>
      </div>
    </SideSheet>
  );

  async function load() {
    setLoading(true);
    try {
      const result = await listAiDatabaseHostProfiles({ pageIndex: 1, pageSize: 100 });
      setProfiles(result.items);
      setSelectedId(current => current || result.items[0]?.id || "");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setLoading(false);
    }
  }

  async function test() {
    setLoading(true);
    try {
      const result = await testAiDatabaseHostProfile({
        profileId: selected?.id,
        driverCode: form.driverCode,
        connectionString: form.connectionString
      });
      Toast.success(result.message || labels.testSuccess);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setLoading(false);
    }
  }

  async function save() {
    if (!form.name.trim()) {
      Toast.warning(labels.name);
      return;
    }

    setLoading(true);
    try {
      if (selected) {
        await updateAiDatabaseHostProfile(selected.id, form);
      } else {
        const id = await createAiDatabaseHostProfile(form);
        setSelectedId(id);
      }
      Toast.success(labels.saveSuccess);
      await load();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setLoading(false);
    }
  }

  async function remove() {
    if (!selected) return;
    setLoading(true);
    try {
      await deleteAiDatabaseHostProfile(selected.id);
      setSelectedId("");
      await load();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setLoading(false);
    }
  }
}
