<template>
  <a-config-provider :locale="antdLocale">
    <router-view />
  </a-config-provider>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { getActiveLocale, getAntdLocale } from "@/i18n";

const antdLocale = computed(() => getAntdLocale(getActiveLocale()));
</script>
