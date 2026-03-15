<template>
  <template v-if="!item.hidden">
    <template v-if="hasOneShowingChild(childrenList, item)">
      <a-menu-item
        :key="resolvePath(onlyOneChild?.path || item.path)"
        @click="go(onlyOneChild?.path || item.path)"
      >
        <template v-if="onlyOneChild?.meta?.icon || item.meta?.icon" #icon>
          <component :is="getIcon(onlyOneChild?.meta?.icon || item.meta?.icon)" />
        </template>
        <span :data-testid="buildMenuTestId(onlyOneChild?.path || item.path)">
          {{ resolveMenuTitle(onlyOneChild ?? item) }}
        </span>
      </a-menu-item>
    </template>

    <a-sub-menu v-else :key="resolvePath(item.path)">
      <template #title>
        <span v-if="item.meta?.icon">
          <component :is="getIcon(item.meta.icon)" style="margin-right: 8px;" />
        </span>
        <span :data-testid="buildMenuTestId(item.path)">{{ resolveMenuTitle(item) }}</span>
      </template>
      <SidebarItem
        v-for="child in childrenList"
        :key="child.path"
        :item="child"
        :base-path="resolvePath(child.path)"
      />
    </a-sub-menu>
  </template>
</template>

<script setup lang="ts">
import { computed, ref, h } from "vue";
import { useRouter } from "vue-router";
import type { RouterVo } from "@/types/api";
import * as antIcons from "@ant-design/icons-vue";
import { isExternal } from "@/utils/validate";
import { resolveRouteTitle } from "@/utils/i18n-navigation";

const props = defineProps<{
  item: RouterVo;
  basePath: string;
}>();

const router = useRouter();
const onlyOneChild = ref<RouterVo | null>(null);
const childrenList = computed<RouterVo[]>(() => (Array.isArray(props.item.children) ? props.item.children : []));

function getIcon(iconName?: string) {
  if (!iconName) return null;
  
  // Handle icons like 'setting' -> 'SettingOutlined'
  // Or handle full icon names like 'SettingOutlined'
  let compName = iconName;
  if (!iconName.endsWith('Outlined') && !iconName.endsWith('Filled') && !iconName.endsWith('TwoTone')) {
    // Convert kebab-case or snake_case to PascalCase
    const pascalCase = iconName.replace(/(^\w|-\w|_\w)/g, (clear) => clear.replace(/-|_/, "").toUpperCase());
    compName = `${pascalCase}Outlined`;
  }
  
  // @ts-ignore
  const iconComponent = antIcons[compName];
  if (iconComponent) {
    return iconComponent;
  }
  
  // Fallback to a default icon if not found
  return antIcons.AppstoreOutlined;
}

function hasOneShowingChild(childrenInput: RouterVo[] | null | undefined, parent: RouterVo) {
  const children = Array.isArray(childrenInput) ? childrenInput : [];
  const showingChildren = children.filter((item) => {
    if (item.hidden) {
      return false;
    } else {
      onlyOneChild.value = item;
      return true;
    }
  });

  if (showingChildren.length === 1 && !parent.alwaysShow) {
    return true;
  }

  if (showingChildren.length === 0) {
    onlyOneChild.value = { ...parent, path: "", noCache: true } as any;
    return true;
  }

  return false;
}

function resolvePath(routePath: string) {
  if (isExternal(routePath)) {
    return routePath;
  }
  if (isExternal(props.basePath)) {
    return props.basePath;
  }
  if (routePath.startsWith("/")) {
    return routePath;
  }
  return props.basePath.endsWith("/")
    ? `${props.basePath}${routePath}`
    : `${props.basePath}/${routePath}`;
}

function go(path: string) {
  const fullPath = resolvePath(path);
  if (isExternal(fullPath)) {
    window.open(fullPath, "_blank");
  } else {
    router.push(fullPath);
  }
}

function buildMenuTestId(path: string) {
  const normalized = resolvePath(path)
    .replace(/[^a-zA-Z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .toLowerCase();
  return `e2e-menu-${normalized || "root"}`;
}

function resolveMenuTitle(routeItem: RouterVo) {
  return resolveRouteTitle(routeItem.meta, routeItem.path, routeItem.meta?.title || routeItem.name);
}
</script>
