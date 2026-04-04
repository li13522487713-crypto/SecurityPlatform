<template>
  <div class="console-home">
    <div class="welcome-section">
      <h1>{{ t("consoleLayout.menuConsoleHome") }}</h1>
      <p class="welcome-desc">{{ t("authPage.brandSubtitle") }}</p>
    </div>

    <a-row :gutter="[16, 16]" style="margin-top: 24px;">
      <a-col :xs="24" :sm="12" :lg="6">
        <a-card hoverable @click="$router.push('/console/apps')">
          <a-statistic :title="t('consoleLayout.menuAppManagement')" value="-" />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :lg="6">
        <a-card hoverable>
          <a-statistic :title="t('consoleLayout.groupRuntime')" value="-" />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :lg="6">
        <a-card hoverable>
          <a-statistic :title="t('consoleLayout.groupTenant')" value="-" />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :lg="6">
        <a-card hoverable>
          <a-statistic :title="t('consoleLayout.groupSystem')" value="-" />
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
const { t } = useI18n();
</script>

<style scoped>
.console-home {
  padding: 24px;
}

.welcome-section h1 {
  font-size: 24px;
  font-weight: 600;
  margin: 0 0 8px;
}

.welcome-desc {
  color: var(--color-text-secondary);
  font-size: 14px;
}
</style>
