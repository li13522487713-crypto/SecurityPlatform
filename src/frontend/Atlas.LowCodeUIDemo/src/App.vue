<template>
  <div class="demo-root">
    <Suspense>
      <AmisDesigner v-model="schema" height="100vh" @save="onSave" />
      <template #fallback>
        <div class="demo-loading">加载设计器模块…</div>
      </template>
    </Suspense>
  </div>
</template>

<script setup lang="ts">
import { defineAsyncComponent, ref } from "vue";
import type { AmisSchema } from "@atlas/lowcode-ui";
import { pageSchema } from "@atlas/lowcode-ui";

const AmisDesigner = defineAsyncComponent(() =>
  import("@atlas/lowcode-ui/designer").then((m) => m.AmisDesigner),
);

const schema = ref<AmisSchema>(
  pageSchema({
    title: "LowCode Demo",
    body: { type: "tpl", tpl: "<p>Hello Atlas LowCodeUI</p>" },
  }),
);

function onSave(s: AmisSchema): void {
  console.info("[LowCodeUIDemo] save", s);
}
</script>

<style>
html,
body,
#app {
  margin: 0;
  height: 100%;
}
</style>

<style scoped>
.demo-root {
  height: 100vh;
  width: 100%;
}

.demo-loading {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100vh;
  color: #646a73;
  font-size: 14px;
}
</style>
