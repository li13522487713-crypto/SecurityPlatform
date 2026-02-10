import { computed, ref } from "vue";

export interface TreeNode {
  key: string;
  title: string;
  children?: TreeNode[];
}

export interface TreeItem {
  id: string;
  name: string;
  parentId?: number | string | null;
}

/**
 * Reusable composable for building and filtering tree data.
 * Replaces duplicated buildTree / filterTree logic in DepartmentsPage and MenusPage.
 */
export function useTreeData<TItem extends TreeItem>(items: () => TItem[]) {
  const treeKeyword = ref("");

  const buildTree = (list: TItem[]): TreeNode[] => {
    const nodeMap = new Map<string, TreeNode>();
    const rootNodes: TreeNode[] = [];

    list.forEach((item) => {
      nodeMap.set(item.id, { key: item.id, title: item.name, children: [] });
    });

    list.forEach((item) => {
      const node = nodeMap.get(item.id);
      if (!node) return;
      if (item.parentId) {
        const parent = nodeMap.get(item.parentId.toString());
        if (parent) {
          parent.children = parent.children ?? [];
          parent.children.push(node);
          return;
        }
      }
      rootNodes.push(node);
    });

    const sortNodes = (nodes: TreeNode[]) => {
      nodes.sort((a, b) => a.title.localeCompare(b.title, "zh-Hans-CN"));
      nodes.forEach((child) => {
        if (child.children && child.children.length > 0) {
          sortNodes(child.children);
        }
      });
    };

    sortNodes(rootNodes);
    return rootNodes;
  };

  const filterTree = (nodes: TreeNode[], keyword: string): TreeNode[] => {
    const matcher = keyword.trim();
    if (!matcher) return nodes;
    const result: TreeNode[] = [];
    nodes.forEach((node) => {
      const children = node.children ? filterTree(node.children, matcher) : [];
      if (node.title.includes(matcher) || children.length > 0) {
        result.push({ ...node, children });
      }
    });
    return result;
  };

  const treeData = computed(() => filterTree(buildTree(items()), treeKeyword.value));

  const expandedKeysForSearch = computed(() => {
    if (!treeKeyword.value.trim()) return [];
    return items().map((item) => item.id);
  });

  const getParentName = (parentId?: number | string | null): string => {
    if (!parentId) return "-";
    const target = items().find((item) => String(item.id) === String(parentId));
    return target?.name ?? `ID ${parentId}`;
  };

  return {
    treeKeyword,
    treeData,
    expandedKeysForSearch,
    buildTree,
    filterTree,
    getParentName
  };
}
