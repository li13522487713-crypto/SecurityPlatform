<template>
  <div class="entity-modeling-panel">
    <div class="panel-header">
      <h4>{{ t("designer.entityModeling.title") }}</h4>
    </div>
    <a-form layout="vertical" class="table-meta-form">
      <a-row :gutter="16">
        <a-col :span="12">
          <a-form-item :label="t('designer.entityModeling.tableKey')" required>
            <a-input v-model:value="tableKey" :disabled="isEditing" placeholder="e.g. orders" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item :label="t('designer.entityModeling.tableName')" required>
            <a-input v-model:value="tableName" placeholder="e.g. Orders" />
          </a-form-item>
        </a-col>
      </a-row>
    </a-form>

    <a-table
      :data-source="fields"
      :pagination="false"
      row-key="tempId"
      size="small"
      bordered
      :scroll="{ x: 1180 }"
    >
      <a-table-column :title="t('designer.entityModeling.fieldName')" data-index="name" width="220px">
        <template #default="{ record }">
          <a-input v-model:value="record.name" size="small" />
        </template>
      </a-table-column>
      <a-table-column :title="t('designer.entityModeling.displayName')" data-index="displayName" width="220px">
        <template #default="{ record }">
          <a-input v-model:value="record.displayName" size="small" />
        </template>
      </a-table-column>
      <a-table-column :title="t('designer.entityModeling.fieldType')" data-index="fieldType" width="160px">
        <template #default="{ record }">
          <a-select v-model:value="record.fieldType" size="small" style="width: 100%">
            <a-select-option v-for="ft in fieldTypes" :key="ft" :value="ft">{{ ft }}</a-select-option>
          </a-select>
        </template>
      </a-table-column>
      <a-table-column :title="t('designer.entityModeling.length')" data-index="length" width="110px">
        <template #default="{ record }">
          <a-input-number v-model:value="record.length" size="small" :min="0" style="width: 100%" />
        </template>
      </a-table-column>
      <a-table-column :title="t('designer.entityModeling.allowNull')" data-index="allowNull" width="90px">
        <template #default="{ record }">
          <a-checkbox v-model:checked="record.allowNull" />
        </template>
      </a-table-column>
      <a-table-column :title="t('designer.entityModeling.isPrimaryKey')" data-index="isPrimaryKey" width="80px">
        <template #default="{ record }">
          <a-checkbox v-model:checked="record.isPrimaryKey" />
        </template>
      </a-table-column>
      <a-table-column :title="t('designer.entityModeling.isUnique')" data-index="isUnique" width="80px">
        <template #default="{ record }">
          <a-checkbox v-model:checked="record.isUnique" />
        </template>
      </a-table-column>
      <a-table-column width="120px">
        <template #default="{ record }">
          <a-button type="link" danger size="small" @click="removeField(record.tempId)">
            {{ t("designer.entityModeling.removeField") }}
          </a-button>
        </template>
      </a-table-column>
    </a-table>

    <div class="panel-footer">
      <a-button @click="addField">{{ t("designer.entityModeling.addField") }}</a-button>
      <a-button type="primary" :loading="saving" @click="handleSave">{{ t("designer.entityModeling.save") }}</a-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from "vue";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";

const { t } = useI18n();

interface Props {
  initialTableKey?: string;
  initialTableName?: string;
  initialFields?: FieldRow[];
  appId?: string;
}

const props = withDefaults(defineProps<Props>(), {
  initialTableKey: "",
  initialTableName: "",
  initialFields: () => [],
  appId: "",
});

const emit = defineEmits<{
  (e: "save", payload: { tableKey: string; displayName: string; fields: FieldRow[] }): void;
}>();

interface FieldRow {
  tempId: string;
  name: string;
  displayName: string;
  fieldType: string;
  length: number;
  allowNull: boolean;
  isPrimaryKey: boolean;
  isUnique: boolean;
}

const fieldTypes = [
  "String", "Text", "Int", "Long", "Decimal", "Boolean",
  "DateTime", "Date", "Time", "Enum", "File", "Image", "Json", "Guid",
];

const isEditing = ref(!!props.initialTableKey);
const tableKey = ref(props.initialTableKey);
const tableName = ref(props.initialTableName);
const saving = ref(false);

let nextTempId = 1;
function makeTempId() {
  return `field-${nextTempId++}`;
}

const fields = reactive<FieldRow[]>(
  props.initialFields.length > 0
    ? props.initialFields.map((f) => ({ ...f, tempId: f.tempId || makeTempId() }))
    : [
        { tempId: makeTempId(), name: "id", displayName: "ID", fieldType: "Long", length: 0, allowNull: false, isPrimaryKey: true, isUnique: true },
        { tempId: makeTempId(), name: "name", displayName: "Name", fieldType: "String", length: 200, allowNull: false, isPrimaryKey: false, isUnique: false },
      ],
);

function addField() {
  fields.push({
    tempId: makeTempId(),
    name: "",
    displayName: "",
    fieldType: "String",
    length: 200,
    allowNull: true,
    isPrimaryKey: false,
    isUnique: false,
  });
}

function removeField(tempId: string) {
  const idx = fields.findIndex((f) => f.tempId === tempId);
  if (idx >= 0) {
    fields.splice(idx, 1);
  }
}

async function handleSave() {
  if (!tableKey.value.trim()) {
    message.warning(t("designer.entityModeling.tableKey") + " required");
    return;
  }
  if (!tableName.value.trim()) {
    message.warning(t("designer.entityModeling.tableName") + " required");
    return;
  }
  const invalidFields = fields.filter((f) => !f.name.trim());
  if (invalidFields.length > 0) {
    message.warning(t("designer.entityModeling.fieldName") + " required");
    return;
  }

  saving.value = true;
  try {
    emit("save", {
      tableKey: tableKey.value,
      displayName: tableName.value,
      fields: [...fields],
    });
    message.success(t("designer.entityModeling.saveSuccess"));
  } catch (error) {
    message.error((error as Error).message || t("designer.entityModeling.saveFailed"));
  } finally {
    saving.value = false;
  }
}
</script>

<style scoped>
.entity-modeling-panel {
  padding: 16px;
}

.panel-header h4 {
  margin: 0 0 16px;
}

.table-meta-form {
  margin-bottom: 16px;
}

.panel-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 16px;
}
</style>
