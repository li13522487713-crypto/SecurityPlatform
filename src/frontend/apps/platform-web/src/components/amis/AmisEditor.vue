<template>
  <div class="amis-editor" :style="{ height }">
    <div class="editor-toolbar">
      <a-space>
        <a-button size="small" @click="formatSchema">{{ t("amisEditor.format") }}</a-button>
        <a-button size="small" type="primary" @click="saveSchema">{{ t("amisEditor.save") }}</a-button>
      </a-space>
    </div>
    <a-textarea
      v-model:value="schemaText"
      :rows="24"
      class="editor-textarea"
      @change="onSchemaChange"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from "vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";

interface Props {
  schema: Record<string, object | string | number | boolean | null>;
  schemaRevision?: number;
  height?: string;
}

const props = withDefaults(defineProps<Props>(), {
  schemaRevision: 0,
  height: "100%",
});

const emit = defineEmits<{
  (e: "change", schema: Record<string, object | string | number | boolean | null>): void;
  (e: "save", schema: Record<string, object | string | number | boolean | null>): void;
}>();

const { t } = useI18n();
const schemaText = ref(JSON.stringify(props.schema, null, 2));

watch(
  () => props.schemaRevision,
  () => {
    schemaText.value = JSON.stringify(props.schema, null, 2);
  }
);

const parseSchema = (): Record<string, object | string | number | boolean | null> | null => {
  try {
    return JSON.parse(schemaText.value) as Record<string, object | string | number | boolean | null>;
  } catch {
    message.error(t("amisEditor.invalidJson"));
    return null;
  }
};

const onSchemaChange = () => {
  const parsed = parseSchema();
  if (parsed) {
    emit("change", parsed);
  }
};

const formatSchema = () => {
  const parsed = parseSchema();
  if (!parsed) {
    return;
  }
  schemaText.value = JSON.stringify(parsed, null, 2);
  emit("change", parsed);
};

const saveSchema = () => {
  const parsed = parseSchema();
  if (!parsed) {
    return;
  }
  emit("save", parsed);
};

defineExpose({
  getSchema: () => parseSchema() ?? props.schema,
});
</script>

<style scoped>
.amis-editor {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.editor-toolbar {
  display: flex;
  justify-content: flex-end;
}

.editor-textarea {
  flex: 1;
}
</style>
