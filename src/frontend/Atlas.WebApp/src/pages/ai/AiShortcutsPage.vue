<template>
  <a-space direction="vertical" style="width: 100%" :size="16">
    <a-card title="快捷命令" :bordered="false">
      <template #extra>
        <a-button type="primary" @click="openCreate">新建命令</a-button>
      </template>

      <a-table :columns="columns" :data-source="commands" :loading="loading" row-key="id" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'enabled'">
            <a-switch :checked="record.isEnabled" @change="toggleEnabled(record, $event)" />
          </template>
          <template v-if="column.key === 'action'">
            <a-space>
              <a-button type="link" @click="openEdit(record)">编辑</a-button>
              <a-popconfirm title="确认删除该命令？" @confirm="handleDelete(record.id)">
                <a-button type="link" danger>删除</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <OnboardingGuide :shortcuts="commands" />

    <a-modal
      v-model:open="modalOpen"
      :title="editingId ? '编辑快捷命令' : '新建快捷命令'"
      :confirm-loading="submitting"
      @ok="submit"
      @cancel="closeModal"
    >
      <a-form ref="formRef" :model="form" layout="vertical" :rules="rules">
        <a-form-item v-if="!editingId" label="命令编码" name="commandKey">
          <a-input v-model:value="form.commandKey" />
        </a-form-item>
        <a-form-item label="名称" name="displayName">
          <a-input v-model:value="form.displayName" />
        </a-form-item>
        <a-form-item label="目标路径" name="targetPath">
          <a-input v-model:value="form.targetPath" />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-input v-model:value="form.description" />
        </a-form-item>
        <a-form-item label="排序" name="sortOrder">
          <a-input-number v-model:value="form.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-space>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import OnboardingGuide from "@/components/ai/OnboardingGuide.vue";
import {
  createAiShortcutCommand,
  deleteAiShortcutCommand,
  getAiShortcutCommands,
  updateAiShortcutCommand,
  type AiShortcutCommandItem
} from "@/services/api-ai-shortcut";

const loading = ref(false);
const commands = ref<AiShortcutCommandItem[]>([]);
const modalOpen = ref(false);
const submitting = ref(false);
const editingId = ref<number | null>(null);
const formRef = ref<FormInstance>();
const form = reactive({
  commandKey: "",
  displayName: "",
  targetPath: "",
  description: "",
  sortOrder: 10
});

const columns = [
  { title: "命令编码", dataIndex: "commandKey", key: "commandKey", width: 180 },
  { title: "名称", dataIndex: "displayName", key: "displayName", width: 180 },
  { title: "目标路径", dataIndex: "targetPath", key: "targetPath" },
  { title: "排序", dataIndex: "sortOrder", key: "sortOrder", width: 100 },
  { title: "启用", key: "enabled", width: 100 },
  { title: "操作", key: "action", width: 140 }
];

const rules = {
  commandKey: [{ required: true, message: "请输入命令编码" }],
  displayName: [{ required: true, message: "请输入名称" }],
  targetPath: [{ required: true, message: "请输入目标路径" }]
};

async function loadCommands() {
  loading.value = true;
  try {
    commands.value = await getAiShortcutCommands();
  } catch (error: unknown) {
    message.error((error as Error).message || "加载快捷命令失败");
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  editingId.value = null;
  Object.assign(form, {
    commandKey: "",
    displayName: "",
    targetPath: "",
    description: "",
    sortOrder: 10
  });
  modalOpen.value = true;
}

function openEdit(command: AiShortcutCommandItem) {
  editingId.value = command.id;
  Object.assign(form, {
    commandKey: command.commandKey,
    displayName: command.displayName,
    targetPath: command.targetPath,
    description: command.description ?? "",
    sortOrder: command.sortOrder
  });
  modalOpen.value = true;
}

function closeModal() {
  modalOpen.value = false;
  formRef.value?.resetFields();
}

async function submit() {
  try {
    await formRef.value?.validate();
  } catch {
    return;
  }

  submitting.value = true;
  try {
    if (editingId.value) {
      const current = commands.value.find((x) => x.id === editingId.value);
      await updateAiShortcutCommand(editingId.value, {
        displayName: form.displayName,
        targetPath: form.targetPath,
        description: form.description || undefined,
        sortOrder: form.sortOrder,
        isEnabled: current?.isEnabled ?? true
      });
      message.success("更新成功");
    } else {
      await createAiShortcutCommand({
        commandKey: form.commandKey,
        displayName: form.displayName,
        targetPath: form.targetPath,
        description: form.description || undefined,
        sortOrder: form.sortOrder
      });
      message.success("创建成功");
    }

    modalOpen.value = false;
    await loadCommands();
  } catch (error: unknown) {
    message.error((error as Error).message || "保存失败");
  } finally {
    submitting.value = false;
  }
}

async function toggleEnabled(command: AiShortcutCommandItem, checked: boolean) {
  try {
    await updateAiShortcutCommand(command.id, {
      displayName: command.displayName,
      targetPath: command.targetPath,
      description: command.description,
      sortOrder: command.sortOrder,
      isEnabled: checked
    });
    await loadCommands();
  } catch (error: unknown) {
    message.error((error as Error).message || "更新状态失败");
  }
}

async function handleDelete(id: number) {
  try {
    await deleteAiShortcutCommand(id);
    message.success("删除成功");
    await loadCommands();
  } catch (error: unknown) {
    message.error((error as Error).message || "删除失败");
  }
}

onMounted(() => {
  void loadCommands();
});
</script>
