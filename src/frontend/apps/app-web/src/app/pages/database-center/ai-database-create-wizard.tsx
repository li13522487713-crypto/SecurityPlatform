import { useEffect, useState } from "react";
import { Button, Input, Select, SideSheet, Space, TextArea, Toast, Typography } from "@douyinfe/semi-ui";
import {
  createDatabaseCenterDatabase,
  type DatabaseCenterCreateDatabaseRequest,
  type DatabaseCenterProvisionMode
} from "../../../services/api-database-center";
import { listAiDatabaseHostProfiles, type AiDatabaseHostProfile } from "../../../services/api-ai-database-host-profiles";
import type { DatabaseCenterLabels } from "./database-center-labels";

const { Text, Title } = Typography;

interface AiDatabaseCreateWizardProps {
  labels: DatabaseCenterLabels;
  visible: boolean;
  workspaceId?: string;
  onClose: () => void;
  onCreated: (id: string) => Promise<void> | void;
}

const steps = ["basic", "hosting", "confirm"] as const;
type WizardStep = typeof steps[number];

export function AiDatabaseCreateWizard({ labels, visible, workspaceId, onClose, onCreated }: AiDatabaseCreateWizardProps) {
  const [step, setStep] = useState<WizardStep>("basic");
  const [hostProfiles, setHostProfiles] = useState<AiDatabaseHostProfile[]>([]);
  const [loading, setLoading] = useState(false);
  const [form, setForm] = useState<DatabaseCenterCreateDatabaseRequest>({
    name: "",
    description: "",
    workspaceId,
    driverCode: "SQLite",
    provisionMode: "Managed",
    hostProfileId: "",
    physicalDatabaseName: "",
    defaultSchemaName: "draft",
    environmentMode: "DraftAndOnline"
  });

  useEffect(() => {
    if (!visible) {
      setStep("basic");
      setForm(previous => ({ ...previous, name: "", description: "", workspaceId }));
      return;
    }

    void loadProfiles();
  }, [visible, workspaceId]);

  const stepIndex = steps.indexOf(step);

  return (
    <SideSheet visible={visible} onCancel={onClose} title={labels.newDatabase} width={720}>
      <Space vertical align="start" style={{ width: "100%" }}>
        <Space>
          {steps.map((item, index) => (
            <Button key={item} theme={item === step ? "solid" : "borderless"} onClick={() => setStep(item)}>
              {index + 1}. {item === "basic" ? labels.basicInfo : item === "hosting" ? labels.hosting : labels.confirm}
            </Button>
          ))}
        </Space>
        {step === "basic" ? (
          <Space vertical align="start" style={{ width: "100%" }}>
            <Text type="tertiary">{labels.wizardBasicHint}</Text>
            <Input placeholder={labels.name} value={form.name} onChange={value => setForm({ ...form, name: value })} />
            <TextArea style={{ width: "100%" }} placeholder={labels.description} value={form.description ?? ""} onChange={(value: string) => setForm({ ...form, description: value })} />
            <Select
              value={form.driverCode}
              onChange={value => setForm({ ...form, driverCode: String(value) })}
              optionList={["SQLite", "MySql", "PostgreSQL", "SqlServer", "Oracle", "Dm", "Kdbndp"].map(value => ({ value, label: value }))}
            />
          </Space>
        ) : null}
        {step === "hosting" ? (
          <Space vertical align="start" style={{ width: "100%" }}>
            <Text type="tertiary">{labels.wizardHostingHint}</Text>
            <Select
              value={form.provisionMode}
              onChange={value => setForm({ ...form, provisionMode: String(value) as DatabaseCenterProvisionMode })}
              optionList={[
                { value: "Managed", label: "Managed" },
                { value: "Existing", label: "Existing" },
                { value: "Attach", label: "Attach" }
              ]}
            />
            <Select
              placeholder={labels.hostProfile}
              value={form.hostProfileId || undefined}
              loading={loading}
              onChange={value => setForm({ ...form, hostProfileId: String(value) })}
              optionList={hostProfiles.map(profile => ({ value: profile.id, label: `${profile.name} / ${profile.driverCode}` }))}
            />
            <Input placeholder={labels.physicalDatabase} value={form.physicalDatabaseName ?? ""} onChange={value => setForm({ ...form, physicalDatabaseName: value })} />
            <Input placeholder={labels.defaultSchema} value={form.defaultSchemaName ?? ""} onChange={value => setForm({ ...form, defaultSchemaName: value })} />
            <Select
              value={form.environmentMode}
              onChange={value => setForm({ ...form, environmentMode: String(value) })}
              optionList={[
                { value: "DraftAndOnline", label: "Draft + Online" },
                { value: "DraftOnly", label: "Draft only" }
              ]}
            />
          </Space>
        ) : null}
        {step === "confirm" ? (
          <Space vertical align="start" style={{ width: "100%" }}>
            <Title heading={5}>{form.name || labels.newDatabase}</Title>
            <Text>{labels.driver}: {form.driverCode}</Text>
            <Text>{labels.provisionMode}: {form.provisionMode}</Text>
            <Text>{labels.hostProfile}: {hostProfiles.find(item => item.id === form.hostProfileId)?.name ?? "-"}</Text>
            <Text>{labels.physicalDatabase}: {form.physicalDatabaseName || "-"}</Text>
            <Text>{labels.defaultSchema}: {form.defaultSchemaName || "-"}</Text>
          </Space>
        ) : null}
        <Space>
          <Button disabled={stepIndex === 0 || loading} onClick={() => setStep(steps[stepIndex - 1])}>{labels.previous}</Button>
          {stepIndex < steps.length - 1 ? (
            <Button theme="solid" disabled={!canContinue()} onClick={() => setStep(steps[stepIndex + 1])}>{labels.next}</Button>
          ) : (
            <Button theme="solid" loading={loading} disabled={!canContinue()} onClick={() => void create()}>{labels.create}</Button>
          )}
          <Button onClick={onClose}>{labels.cancel}</Button>
        </Space>
      </Space>
    </SideSheet>
  );

  async function loadProfiles() {
    setLoading(true);
    try {
      const result = await listAiDatabaseHostProfiles({ pageIndex: 1, pageSize: 100, activeOnly: true });
      setHostProfiles(result.items);
      setForm(current => ({ ...current, hostProfileId: current.hostProfileId || result.items.find(item => item.isDefault)?.id || result.items[0]?.id || "" }));
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setLoading(false);
    }
  }

  function canContinue(): boolean {
    if (step === "basic") return Boolean(form.name.trim() && form.driverCode);
    if (step === "hosting") return Boolean(form.provisionMode);
    return Boolean(form.name.trim());
  }

  async function create() {
    setLoading(true);
    try {
      const id = await createDatabaseCenterDatabase({ ...form, workspaceId });
      Toast.success(labels.createSuccess);
      await onCreated(id);
      onClose();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setLoading(false);
    }
  }
}
