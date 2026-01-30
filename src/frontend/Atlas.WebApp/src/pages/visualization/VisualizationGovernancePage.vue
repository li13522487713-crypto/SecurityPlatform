<template>
  <a-card title="治理中心（骨架）" class="page-card">
    <a-row :gutter="[16, 16]">
      <a-col :span="12">
        <a-card size="small" title="口径管理">
          <p>列出口径、版本、审批状态。</p>
          <a-button type="primary" size="small">新建口径</a-button>
        </a-card>
      </a-col>
      <a-col :span="12">
        <a-card size="small" title="数据源清单">
          <p>展示数据源、刷新频率、负责人。</p>
          <a-button size="small">导出清单</a-button>
        </a-card>
      </a-col>
      <a-col :span="12">
        <a-card size="small" title="质量检查">
          <p>质量抽检结果与异常记录。</p>
          <a-button size="small">查看详情</a-button>
        </a-card>
      </a-col>
      <a-col :span="12">
        <a-card size="small" title="审计与留痕">
          <p>重要操作、发布、导出等留痕查看。</p>
          <a-button size="small">查看审计</a-button>
        </a-card>
      </a-col>
    </a-row>
  </a-card>
</template>

<script setup lang="ts">
// 治理中心骨架，无额外逻辑
</script>
