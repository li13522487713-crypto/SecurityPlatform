<template>
  <div class="lf-form-designer">
    <a-alert
      type="info"
      show-icon
      message="LF 低代码表单设计（JSON 模式）"
      description="当前使用 JSON 编辑模式，后续可替换为可视化表单设计器组件。"
    />
    <div class="designer-shell">
      <div class="designer-panel">
        <div v-if="!vformReady" class="designer-loading">表单设计器加载中...</div>
        <v-form-designer v-else ref="designerRef"></v-form-designer>
      </div>
      <div class="json-panel">
        <a-form layout="vertical" class="form-container">
          <a-form-item label="表单 JSON">
            <a-textarea
              v-model:value="formJsonText"
              :rows="14"
              placeholder="请输入表单 JSON"
            />
          </a-form-item>
          <a-space>
            <a-button type="primary" @click="applyJson">导入 JSON</a-button>
            <a-button @click="syncFromDesigner">同步字段</a-button>
            <a-button @click="exportJson">导出 JSON</a-button>
            <a-button @click="formatJson">格式化</a-button>
          </a-space>
        </a-form>
      </div>
    </div>
    <a-divider />
    <div class="field-preview">
      <div class="field-title">字段列表（用于条件与权限配置）</div>
      <a-table
        :columns="columns"
        :data-source="formFields"
        :pagination="false"
        row-key="fieldId"
        size="small"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { getCurrentInstance, onMounted, ref, watch } from 'vue';
import type { LfFormField, FormJson, FormWidget } from '@/types/approval-definition';
import { message } from 'ant-design-vue';

const props = defineProps<{
  modelValue?: FormJson;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: FormJson];
  'update:formFields': [fields: LfFormField[]];
}>();

const formJsonText = ref('');
const formFields = ref<LfFormField[]>([]);
const designerRef = ref<VFormDesignerInstance | null>(null);
const vformReady = ref(false);

const columns = [
  { title: '字段ID', dataIndex: 'fieldId' },
  { title: '字段名称', dataIndex: 'fieldName' },
  { title: '字段类型', dataIndex: 'fieldType' },
  { title: '值类型', dataIndex: 'valueType' }
];

watch(
  () => props.modelValue,
  (value) => {
    if (value) {
      formJsonText.value = JSON.stringify(value, null, 2);
      formFields.value = extractFields(value);
      if (designerRef.value) {
        designerRef.value.setFormJson(value);
      }
    } else {
      formJsonText.value = '';
      formFields.value = [];
    }
  },
  { immediate: true }
);

const applyJson = () => {
  if (!formJsonText.value.trim()) {
    emit('update:modelValue', { widgetList: [] });
    emit('update:formFields', []);
    formFields.value = [];
    return;
  }

  const parsed = parseFormJson(formJsonText.value);
  if (!parsed) {
    message.error('表单 JSON 解析失败，请检查格式');
    return;
  }

  const fields = extractFields(parsed);
  formFields.value = fields;
  emit('update:modelValue', parsed);
  emit('update:formFields', fields);
  if (designerRef.value) {
    designerRef.value.setFormJson(parsed);
  }
  message.success('表单 JSON 已应用');
};

const exportJson = () => {
  if (!designerRef.value) return;
  const formJson = designerRef.value.getFormJson();
  formJsonText.value = JSON.stringify(formJson, null, 2);
  const fields = extractFields(formJson);
  formFields.value = fields;
  emit('update:modelValue', formJson);
  emit('update:formFields', fields);
};

const syncFromDesigner = () => {
  if (!designerRef.value) return;
  const formJson = designerRef.value.getFormJson();
  const fields = extractFields(formJson);
  formFields.value = fields;
  emit('update:modelValue', formJson);
  emit('update:formFields', fields);
};

const formatJson = () => {
  if (!formJsonText.value.trim()) return;
  const parsed = parseFormJson(formJsonText.value);
  if (!parsed) {
    message.error('格式化失败，请检查 JSON');
    return;
  }
  formJsonText.value = JSON.stringify(parsed, null, 2);
};

const extractFields = (formJson: FormJson): LfFormField[] => {
  const widgetList = formJson.widgetList;
  if (!Array.isArray(widgetList)) return [];

  const fields: LfFormField[] = [];
  const traverse = (widgets: FormWidget[]) => {
    widgets.forEach((widget) => {
      if (!widget) return;
      if (Array.isArray(widget.widgetList)) {
        traverse(widget.widgetList);
        return;
      }
      const fieldId = widget.id ?? widget.options?.name;
      if (!fieldId) return;
      const fieldName = widget.options?.label ?? widget.label ?? fieldId;
      fields.push({
        fieldId,
        fieldName,
        fieldType: widget.type ?? 'unknown',
        valueType: widget.options?.fieldType ?? 'String',
        options: widget.options?.options ?? []
      });
    });
  };

  traverse(widgetList);
  return fields;
};

const parseFormJson = (value: string): FormJson | null => {
  try {
    const parsed = JSON.parse(value) as FormJson;
    if (!parsed || typeof parsed !== 'object') return null;
    if (parsed.widgetList && !Array.isArray(parsed.widgetList)) return null;
    return parsed;
  } catch {
    return null;
  }
};

onMounted(() => {
  void initVForm();
  if (props.modelValue && designerRef.value) {
    designerRef.value.setFormJson(props.modelValue);
  }
});

const initVForm = async () => {
  const instance = getCurrentInstance();
  if (!instance) return;

  const mod = await import('vform3-builds');
  await import('vform3-builds/dist/designer.style.css');

  const app = instance.appContext.app;
  const globals = app.config.globalProperties as { __vform3_installed__?: boolean };
  if (!globals.__vform3_installed__) {
    app.use(mod.default);
    globals.__vform3_installed__ = true;
  }
  vformReady.value = true;
};

type VFormDesignerInstance = {
  getFormJson: () => FormJson;
  setFormJson: (formJson: FormJson) => void;
};
</script>

<style scoped>
.lf-form-designer {
  background: #fff;
  padding: 16px;
  border: 1px solid #f0f0f0;
  border-radius: 6px;
}

.designer-shell {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 12px;
  margin-top: 12px;
}

.designer-panel {
  border: 1px solid #f0f0f0;
  border-radius: 6px;
  min-height: 520px;
  overflow: hidden;
}
.designer-loading {
  padding: 16px;
  color: #8c8c8c;
}

.json-panel {
  border: 1px solid #f0f0f0;
  border-radius: 6px;
  padding: 12px;
}

.field-preview {
  margin-top: 12px;
}

.field-title {
  font-weight: 600;
  margin-bottom: 8px;
}
</style>
