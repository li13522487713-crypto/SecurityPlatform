<template>
  <a-drawer
    v-model:open="open"
    :title="t('erd.referencesTitle', { tableKey: props.tableKey })"
    :width="480"
    placement="right"
  >
    <a-spin :spinning="loading">
      <template v-if="!loading && result">
        <!-- 绑定审批流 -->
        <div class="ref-section">
          <div class="ref-section-title">
            <ApartmentOutlined />
            {{ t('erd.boundApprovalFlow') }}
          </div>
          <div v-if="result.boundApprovalFlow" class="ref-item">
            <a-tag color="blue">{{ result.boundApprovalFlow.status }}</a-tag>
            {{ result.boundApprovalFlow.name }}
          </div>
          <a-empty v-else :description="t('erd.noApprovalFlow')" :image="Empty.PRESENTED_IMAGE_SIMPLE" />
        </div>

        <!-- 引用此实体的表单 -->
        <a-divider />
        <div class="ref-section">
          <div class="ref-section-title">
            <FormOutlined />
            {{ t('erd.referencedForms') }}
            <a-badge :count="result.formDefinitions.length" :overflow-count="99" />
          </div>
          <a-list
            :data-source="result.formDefinitions"
            size="small"
          >
            <template #renderItem="{ item }">
              <a-list-item>
                <a-list-item-meta :title="item.name" :description="item.description ?? item.category" />
                <template #extra>
                  <a-tag>{{ item.status }}</a-tag>
                </template>
              </a-list-item>
            </template>
            <template #empty>
              <a-empty :description="t('erd.noForms')" :image="Empty.PRESENTED_IMAGE_SIMPLE" />
            </template>
          </a-list>
        </div>

        <!-- 引用此实体的页面 -->
        <a-divider />
        <div class="ref-section">
          <div class="ref-section-title">
            <FileOutlined />
            {{ t('erd.referencedPages') }}
            <a-badge :count="result.lowCodePages.length" :overflow-count="99" />
          </div>
          <a-list
            :data-source="result.lowCodePages"
            size="small"
          >
            <template #renderItem="{ item }">
              <a-list-item>
                <a-list-item-meta :title="item.name" :description="item.pageKey" />
              </a-list-item>
            </template>
            <template #empty>
              <a-empty :description="t('erd.noPages')" :image="Empty.PRESENTED_IMAGE_SIMPLE" />
            </template>
          </a-list>
        </div>
      </template>
    </a-spin>
  </a-drawer>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { Empty, message } from 'ant-design-vue';
import { ApartmentOutlined, FormOutlined, FileOutlined } from '@ant-design/icons-vue';
import { getEntityReferences } from '@/services/api-system';
import type { EntityReferenceResult } from '@/types/api';

const { t } = useI18n();

const props = defineProps<{
  tableKey: string;
}>();

const open = defineModel<boolean>('open', { required: true });

const loading = ref(false);
const result = ref<EntityReferenceResult | null>(null);

watch(open, async (visible) => {
  if (!visible || !props.tableKey) return;
  loading.value = true;
  result.value = null;
  try {
    result.value = await getEntityReferences(props.tableKey);
  } catch (e) {
    message.error((e as Error).message || t('erd.loadReferencesFailed'));
  } finally {
    loading.value = false;
  }
});
</script>

<style scoped>
.ref-section {
  margin-bottom: 8px;
}
.ref-section-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  margin-bottom: 8px;
  color: #333;
}
.ref-item {
  padding: 6px 0;
}
</style>
