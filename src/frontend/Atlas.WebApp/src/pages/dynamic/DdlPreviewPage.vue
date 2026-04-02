<template>
  <div class="ddl-preview-page">
    <a-page-header :title="t('dynamic.ddlPreview')" :sub-title="tableKey">
      <template #extra>
        <a-button @click="handlePreview" :loading="loading" type="primary">
          {{ t('dynamic.generatePreview') }}
        </a-button>
      </template>
    </a-page-header>

    <a-row :gutter="16">
      <a-col :span="16">
        <a-card :title="t('dynamic.upScript')" :bordered="false">
          <pre class="ddl-script">{{ previewResult?.upScript ?? '-- ' + t('dynamic.noPreview') }}</pre>
        </a-card>

        <a-card
          v-if="previewResult?.downHint"
          :title="t('dynamic.downHint')"
          :bordered="false"
          style="margin-top: 16px"
        >
          <pre class="ddl-script ddl-rollback">{{ previewResult.downHint }}</pre>
        </a-card>
      </a-col>

      <a-col :span="8">
        <a-card :title="t('dynamic.warnings')" :bordered="false">
          <a-empty
            v-if="!previewResult?.warnings?.length && !previewResult?.capabilityWarnings?.length"
            :description="t('dynamic.noWarnings')"
          />
          <a-list
            v-else
            size="small"
            :data-source="allWarnings"
          >
            <template #renderItem="{ item }">
              <a-list-item>
                <a-alert
                  :type="item.type"
                  :message="item.message"
                  show-icon
                  :banner="true"
                />
              </a-list-item>
            </template>
          </a-list>
        </a-card>

        <a-card
          v-if="compatResult"
          :title="t('dynamic.compatibilityCheck')"
          :bordered="false"
          style="margin-top: 16px"
        >
          <a-result
            v-if="compatResult.isCompatible"
            status="success"
            :title="t('dynamic.compatible')"
          />
          <a-result
            v-else
            status="warning"
            :title="t('dynamic.incompatible')"
          />

          <a-list
            v-if="compatResult.highRiskWarnings.length"
            size="small"
            :data-source="compatResult.highRiskWarnings"
          >
            <template #renderItem="{ item }">
              <a-list-item>
                <a-alert
                  type="error"
                  :message="`[${item.riskLevel}] ${item.objectName}: ${item.description}`"
                  show-icon
                  :banner="true"
                />
              </a-list-item>
            </template>
          </a-list>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import {
  previewDdl,
  checkCompatibility,
} from '@/services/schema-publish'
import type {
  DdlPreviewResult,
  SchemaCompatibilityResult,
} from '@/types/schema-publish'

const { t } = useI18n()
const route = useRoute()
const tableKey = computed(() => String(route.params.tableKey ?? ''))

const loading = ref(false)
const previewResult = ref<DdlPreviewResult | null>(null)
const compatResult = ref<SchemaCompatibilityResult | null>(null)

const allWarnings = computed(() => {
  const items: { type: 'warning' | 'error'; message: string }[] = []
  if (previewResult.value?.warnings) {
    for (const w of previewResult.value.warnings) {
      items.push({ type: 'warning', message: w })
    }
  }
  if (previewResult.value?.capabilityWarnings) {
    for (const cw of previewResult.value.capabilityWarnings) {
      items.push({ type: 'error', message: `[${cw.dbType}] ${cw.feature}: ${cw.description}` })
    }
  }
  return items
})

async function handlePreview() {
  loading.value = true
  try {
    const request = {
      tableKey: tableKey.value,
      addFields: null,
      updateFields: null,
      removeFields: null,
      addIndexes: null,
      removeIndexes: null,
    }

    const [ddlRes, compatRes] = await Promise.all([
      previewDdl(request),
      checkCompatibility(request),
    ])

    if (ddlRes.success && ddlRes.data) {
      previewResult.value = ddlRes.data
    }
    if (compatRes.success && compatRes.data) {
      compatResult.value = compatRes.data
    }
  } finally {
    loading.value = false
  }
}

onMounted(handlePreview)
</script>

<style scoped>
.ddl-preview-page {
  padding: 16px;
}
.ddl-script {
  background: #f5f5f5;
  padding: 12px;
  border-radius: 4px;
  font-family: 'Cascadia Code', 'Fira Code', monospace;
  font-size: 13px;
  line-height: 1.6;
  white-space: pre-wrap;
  overflow-x: auto;
  max-height: 500px;
}
.ddl-rollback {
  background: #fff7e6;
}
</style>
