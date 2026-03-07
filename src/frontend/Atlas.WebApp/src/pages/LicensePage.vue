<template>
  <div class="license-page">
    <a-page-header title="授权管理" sub-title="查看当前授权状态、激活证书或续签" />

    <div class="license-content">
      <!-- 授权状态卡片 -->
      <a-row :gutter="[16, 16]">
        <a-col :xs="24" :md="12">
          <a-card title="授权状态" :loading="loading">
            <template #extra>
              <a-tag :color="statusColor">{{ statusLabel }}</a-tag>
            </template>

            <a-descriptions :column="1" bordered size="small">
              <a-descriptions-item label="授权版本">
                <a-tag :color="editionColor">{{ editionLabel }}</a-tag>
              </a-descriptions-item>
              <a-descriptions-item label="有效期">
                <template v-if="licenseStatus?.isPermanent">
                  <a-tag color="green">永久授权</a-tag>
                </template>
                <template v-else-if="licenseStatus?.expiresAt">
                  {{ formatDate(licenseStatus.expiresAt) }}
                  <a-tag v-if="licenseStatus.remainingDays !== null" :color="remainingDaysColor" style="margin-left: 8px">
                    剩余 {{ licenseStatus.remainingDays }} 天
                  </a-tag>
                </template>
                <template v-else>—</template>
              </a-descriptions-item>
              <a-descriptions-item label="颁发时间">
                {{ licenseStatus?.issuedAt ? formatDate(licenseStatus.issuedAt) : '—' }}
              </a-descriptions-item>
              <a-descriptions-item label="机器绑定">
                <template v-if="!licenseStatus?.machineBound">
                  <a-tag color="default">未绑定（任意机器可用）</a-tag>
                </template>
                <template v-else-if="licenseStatus?.machineMatched">
                  <a-tag color="green">已绑定（当前机器匹配）</a-tag>
                </template>
                <template v-else>
                  <a-tag color="red">已绑定（当前机器不匹配）</a-tag>
                </template>
              </a-descriptions-item>
            </a-descriptions>
          </a-card>
        </a-col>

        <!-- 机器码 -->
        <a-col :xs="24" :md="12">
          <a-card title="当前机器码" :loading="fingerprintLoading">
            <template #extra>
              <a-tooltip title="机器码用于向颁发方申请机器绑定证书">
                <QuestionCircleOutlined />
              </a-tooltip>
            </template>
            <p style="font-size: 12px; color: #666; margin-bottom: 12px">
              将以下机器码提供给颁发方，用于生成绑定本机的授权证书。
            </p>
            <a-input-group compact>
              <a-input
                :value="fingerprint"
                readonly
                style="flex: 1; font-family: monospace; font-size: 12px"
              />
              <a-button @click="copyFingerprint">
                <template #icon><CopyOutlined /></template>
                复制
              </a-button>
            </a-input-group>
          </a-card>
        </a-col>
      </a-row>

      <!-- 功能限额 -->
      <a-row :gutter="[16, 16]" style="margin-top: 16px">
        <a-col :xs="24" :md="12">
          <a-card title="功能列表">
            <a-list :data-source="featureList" size="small">
              <template #renderItem="{ item }">
                <a-list-item>
                  <span>{{ item.label }}</span>
                  <template #actions>
                    <a-tag :color="item.enabled ? 'green' : 'default'">
                      {{ item.enabled ? '已开启' : '未开启' }}
                    </a-tag>
                  </template>
                </a-list-item>
              </template>
            </a-list>
          </a-card>
        </a-col>

        <a-col :xs="24" :md="12">
          <a-card title="数量限额">
            <a-list :data-source="limitList" size="small">
              <template #renderItem="{ item }">
                <a-list-item>
                  <span>{{ item.label }}</span>
                  <template #actions>
                    <span style="font-weight: 600">
                      {{ item.value === -1 ? '不限' : item.value }}
                    </span>
                  </template>
                </a-list-item>
              </template>
            </a-list>
          </a-card>
        </a-col>
      </a-row>

      <!-- 激活/续签证书 -->
      <a-card title="激活 / 续签证书" style="margin-top: 16px">
        <a-alert
          v-if="activateResult"
          :type="activateResult.success ? 'success' : 'error'"
          :message="activateResult.message"
          closable
          style="margin-bottom: 16px"
          @close="activateResult = null"
        />
        <p style="color: #666; margin-bottom: 12px">
          上传 <code>.atlaslicense</code> 证书文件激活平台，或使用新证书完成续签/升级。
        </p>
        <a-upload
          :before-upload="handleFileSelect"
          :show-upload-list="false"
          accept=".atlaslicense,.lic,.txt"
        >
          <a-button :loading="activating">
            <template #icon><UploadOutlined /></template>
            选择证书文件
          </a-button>
        </a-upload>
      </a-card>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import {
  QuestionCircleOutlined,
  CopyOutlined,
  UploadOutlined,
} from '@ant-design/icons-vue'
import type { LicenseStatus } from '@/types/api'
import { getLicenseStatus, getMachineFingerprint, activateLicense } from '@/services/api-license'

interface LicenseApiError extends Error {
  payload?: {
    message?: string
  } | null
}

const loading = ref(false)
const fingerprintLoading = ref(false)
const activating = ref(false)
const licenseStatus = ref<LicenseStatus | null>(null)
const fingerprint = ref('')
const activateResult = ref<{ success: boolean; message: string } | null>(null)

const statusColor = computed(() => {
  switch (licenseStatus.value?.status) {
    case 'Active': return 'green'
    case 'Expired': return 'red'
    case 'Invalid': return 'red'
    default: return 'default'
  }
})

const statusLabel = computed(() => {
  switch (licenseStatus.value?.status) {
    case 'Active': return '有效'
    case 'Expired': return '已过期'
    case 'Invalid': return '无效'
    default: return '未激活'
  }
})

const editionColor = computed(() => {
  switch (licenseStatus.value?.edition) {
    case 'Enterprise': return 'purple'
    case 'Pro': return 'blue'
    default: return 'orange'
  }
})

const editionLabel = computed(() => {
  switch (licenseStatus.value?.edition) {
    case 'Enterprise': return '企业版'
    case 'Pro': return '专业版'
    default: return '试用版'
  }
})

const remainingDaysColor = computed(() => {
  const days = licenseStatus.value?.remainingDays
  if (days === null || days === undefined) return 'default'
  if (days <= 7) return 'red'
  if (days <= 30) return 'orange'
  return 'green'
})

const featureMap: Record<string, string> = {
  lowCode: '低代码应用',
  workflow: '工作流引擎',
  approval: '审批流',
  alert: '告警管理',
  offlineDeploy: '离线部署',
  multiTenant: '多租户',
  audit: '审计日志',
}

const limitMap: Record<string, string> = {
  maxApps: '最大应用数',
  maxUsers: '最大用户数',
  maxTenants: '最大租户数',
  maxDataSources: '最大数据源数',
  auditRetentionDays: '审计日志保留天数',
}

const featureList = computed(() => {
  const features = licenseStatus.value?.features ?? {}
  return Object.entries(featureMap).map(([key, label]) => ({
    key,
    label,
    enabled: features[key] === true,
  }))
})

const limitList = computed(() => {
  const limits = licenseStatus.value?.limits ?? {}
  return Object.entries(limitMap).map(([key, label]) => ({
    key,
    label,
    value: limits[key] ?? 0,
  }))
})

function formatDate(dateStr: string): string {
  try {
    return new Date(dateStr).toLocaleDateString('zh-CN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    })
  } catch {
    return dateStr
  }
}

async function loadStatus() {
  loading.value = true
  try {
    licenseStatus.value = await getLicenseStatus()
  } catch {
    message.error('获取授权状态失败')
  } finally {
    loading.value = false
  }
}

async function loadFingerprint() {
  fingerprintLoading.value = true
  try {
    fingerprint.value = await getMachineFingerprint()
  } catch {
    // 忽略错误，机器码不影响主功能
  } finally {
    fingerprintLoading.value = false
  }
}

async function copyFingerprint() {
  if (!fingerprint.value) return
  try {
    await navigator.clipboard.writeText(fingerprint.value)
    message.success('机器码已复制到剪贴板')
  } catch {
    message.error('复制失败，请手动选择并复制')
  }
}

async function handleFileSelect(file: File): Promise<false> {
  activating.value = true
  activateResult.value = null

  let content = ''
  try {
    content = await readFileAsText(file)
  } catch (error) {
    activateResult.value = {
      success: false,
      message: error instanceof Error ? error.message : '文件读取失败，请重试',
    }
    activating.value = false
    return false
  }

  try {
    const resp = await activateLicense(content)
    if (resp.success) {
      activateResult.value = { success: true, message: resp.data?.message ?? resp.message ?? '证书激活成功' }
      await loadStatus()
    } else {
      activateResult.value = { success: false, message: resp.message || '证书激活失败' }
    }
  } catch (error) {
    const requestError = error as LicenseApiError
    const detailMessage =
      requestError?.payload?.message ??
      (error instanceof Error ? error.message : '')
    activateResult.value = {
      success: false,
      message: detailMessage || '证书激活失败，请重试',
    }
  } finally {
    activating.value = false
  }
  return false
}

function readFileAsText(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = (e) => resolve((e.target?.result as string) ?? '')
    reader.onerror = () => reject(new Error('文件读取失败，请重试'))
    reader.readAsText(file)
  })
}

onMounted(async () => {
  await Promise.all([loadStatus(), loadFingerprint()])
})
</script>

<style scoped>
.license-page {
  padding: 24px;
  min-height: 100%;
}

.license-content {
  max-width: 1200px;
}
</style>
