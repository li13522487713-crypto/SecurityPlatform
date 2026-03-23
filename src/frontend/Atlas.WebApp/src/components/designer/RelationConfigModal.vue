<template>
  <a-modal
    :open="open"
    :title="t('relationConfigModal.title')"
    width="560"
    :confirm-loading="false"
    :destroy-on-close="true"
    @ok="handleOk"
    @cancel="emit('cancel')"
  >
    <a-form
      ref="formRef"
      :model="form"
      layout="vertical"
      :rules="formRules"
    >
      <!-- 关系类型 -->
      <a-form-item :label="t('relationConfigModal.relationType')" name="relationType">
        <a-select v-model:value="form.relationType" :placeholder="t('common.select')">
          <a-select-option value="MasterDetail">MasterDetail（主从）</a-select-option>
          <a-select-option value="Lookup">Lookup（查找）</a-select-option>
          <a-select-option value="PolymorphicLookup">PolymorphicLookup（多态查找）</a-select-option>
        </a-select>
      </a-form-item>

      <!-- 基数 -->
      <a-form-item :label="t('relationConfigModal.multiplicity')" name="multiplicity">
        <a-radio-group v-model:value="form.multiplicity" button-style="solid">
          <a-radio-button value="OneToOne">1 : 1</a-radio-button>
          <a-radio-button value="OneToMany">1 : N</a-radio-button>
          <a-radio-button value="ManyToMany">M : N</a-radio-button>
        </a-radio-group>
      </a-form-item>

      <!-- 源字段 -->
      <a-row :gutter="12">
        <a-col :span="12">
          <a-form-item :label="`${t('relationConfigModal.sourceField')} (${sourceTableKey})`" name="sourceField">
            <a-input v-model:value="form.sourceField" placeholder="id" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item :label="`${t('relationConfigModal.targetField')} (${targetTableKey})`" name="targetField">
            <a-input v-model:value="form.targetField" :placeholder="`${sourceTableKey}Id`" />
          </a-form-item>
        </a-col>
      </a-row>

      <!-- 删除行为 -->
      <a-form-item :label="t('relationConfigModal.onDeleteAction')" name="onDeleteAction">
        <a-select v-model:value="form.onDeleteAction" :placeholder="t('common.select')">
          <a-select-option value="NoAction">NoAction（无动作）</a-select-option>
          <a-select-option value="Cascade">Cascade（级联删除）</a-select-option>
          <a-select-option value="SetNull">SetNull（置空）</a-select-option>
          <a-select-option value="Restrict">Restrict（限制）</a-select-option>
        </a-select>
      </a-form-item>

      <!-- 启用 Rollup -->
      <a-form-item :label="t('relationConfigModal.enableRollup')">
        <a-switch v-model:checked="form.enableRollup" />
        <span style="margin-left: 8px; font-size: 12px; color: #888">
          {{ t("relationConfigModal.enableRollupHint") }}
        </span>
      </a-form-item>

      <!-- Rollup 配置面板（折叠） -->
      <template v-if="form.enableRollup">
        <a-divider style="margin: 8px 0">{{ t("relationConfigModal.rollupConfig") }}</a-divider>
        <RollupConfigPanel
          v-model:value="form.rollupDefinitionsJson"
          :source-table-key="sourceTableKey"
          :target-table-key="targetTableKey"
        />
      </template>
    </a-form>
  </a-modal>
</template>

<script setup lang="ts">
import { reactive, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import type { Rule } from "ant-design-vue/es/form";
import type { DynamicRelationDefinition } from "@/types/dynamic-tables";
import RollupConfigPanel from "./RollupConfigPanel.vue";

const props = defineProps<{
  open: boolean;
  sourceTableKey: string;
  targetTableKey: string;
  initialValue?: DynamicRelationDefinition;
}>();

const emit = defineEmits<{
  (e: "update:open", value: boolean): void;
  (e: "confirm", definition: DynamicRelationDefinition): void;
  (e: "cancel"): void;
}>();

const { t } = useI18n();

interface RelationForm {
  relationType: string;
  multiplicity: "OneToOne" | "OneToMany" | "ManyToMany";
  sourceField: string;
  targetField: string;
  onDeleteAction: "NoAction" | "Cascade" | "SetNull" | "Restrict";
  enableRollup: boolean;
  rollupDefinitionsJson: string;
}

const formRef = ref();

const defaultForm = (): RelationForm => ({
  relationType: "MasterDetail",
  multiplicity: "OneToMany",
  sourceField: "id",
  targetField: `${props.sourceTableKey}Id`,
  onDeleteAction: "NoAction",
  enableRollup: false,
  rollupDefinitionsJson: "[]"
});

const form = reactive<RelationForm>(defaultForm());

watch(
  () => props.open,
  (open) => {
    if (open) {
      const iv = props.initialValue;
      if (iv) {
        form.relationType = iv.relationType ?? "MasterDetail";
        form.multiplicity = (iv.multiplicity as RelationForm["multiplicity"]) ?? "OneToMany";
        form.sourceField = iv.sourceField ?? "id";
        form.targetField = iv.targetField ?? `${props.sourceTableKey}Id`;
        form.onDeleteAction = (iv.onDeleteAction as RelationForm["onDeleteAction"]) ?? "NoAction";
        form.enableRollup = iv.enableRollup ?? false;
        form.rollupDefinitionsJson = iv.rollupDefinitionsJson ?? "[]";
      } else {
        Object.assign(form, defaultForm());
        form.targetField = `${props.sourceTableKey}Id`;
      }
    }
  },
  { immediate: true }
);

const formRules: Record<string, Rule[]> = {
  relationType: [{ required: true, message: t("validation.required") }],
  sourceField: [{ required: true, message: t("validation.required") }],
  targetField: [{ required: true, message: t("validation.required") }]
};

async function handleOk() {
  try {
    await formRef.value?.validate();
  } catch {
    return;
  }

  const definition: DynamicRelationDefinition = {
    relatedTableKey: props.targetTableKey,
    sourceField: form.sourceField,
    targetField: form.targetField,
    relationType: form.relationType,
    multiplicity: form.multiplicity,
    onDeleteAction: form.onDeleteAction,
    enableRollup: form.enableRollup,
    rollupDefinitionsJson: form.enableRollup ? form.rollupDefinitionsJson : null
  };
  emit("confirm", definition);
}
</script>
