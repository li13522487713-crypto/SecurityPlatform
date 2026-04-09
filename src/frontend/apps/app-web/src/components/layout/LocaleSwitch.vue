<template>
  <SharedLocaleSwitch
    :current-locale="currentLocale"
    :supported-locales="SUPPORTED_LOCALES"
    :ariaLabel="t('layout.switchLanguage')"
    :get-locale-label="(locale: string) => getLocaleLabel(locale as SupportedLocale)"
    @select="(locale: string) => switchLocale(locale as SupportedLocale)"
  />
</template>

<script setup lang="ts">
import { computed } from "vue";
import { LocaleSwitch as SharedLocaleSwitch } from "@atlas/shared-ui";
import { useI18n } from "vue-i18n";
import { getActiveLocale, getLocaleLabel, setLocale, SUPPORTED_LOCALES, type SupportedLocale } from "@/i18n";

const { t } = useI18n();

const currentLocale = computed(() => getActiveLocale());

function switchLocale(locale: SupportedLocale) {
  if (locale === currentLocale.value) return;
  setLocale(locale);
}
</script>
