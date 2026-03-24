<template>
  <div class="mock-designer-page">
    <DesignerLayout />
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue';
import { useDesignerStore } from './custom-designer/core/store';
import DesignerLayout from './custom-designer/DesignerLayout.vue';

const store = useDesignerStore();

onMounted(() => {
  // 默认示例：“客户管理页面” schema
  store.setSchema({
    id: 'page_1',
    type: 'page',
    name: '客户管理页面',
    props: {},
    styles: { padding: '24px', backgroundColor: '#f0f2f5', minHeight: '100vh', display: 'flex', flexDirection: 'column' },
    children: [
      {
        id: 'card_search',
        type: 'card',
        name: '搜索筛选区',
        props: { title: '客户查询', bordered: false },
        styles: { marginBottom: '24px', borderRadius: '8px' },
        children: [
          {
            id: 'container_filters',
            type: 'container',
            name: '过滤条件',
            props: {},
            styles: { display: 'flex', gap: '16px', flexWrap: 'wrap', border: 'none', padding: '0', backgroundColor: 'transparent' },
            children: [
              { id: 'input_name', type: 'input', name: '客户名称', props: { placeholder: '请输入客户名称' }, styles: { width: '220px', marginBottom: '0' } },
              { id: 'select_status', type: 'select', name: '状态', props: { placeholder: '请选择状态', options: [{label:'正常',value:'1'}, {label:'锁定',value:'2'}] }, styles: { width: '180px', marginBottom: '0' } },
              { id: 'btn_search', type: 'button', name: '查询', props: { text: '查询', type: 'primary' }, styles: { margin: '0' } },
              { id: 'btn_reset', type: 'button', name: '重置', props: { text: '重置', type: 'default' }, styles: { margin: '0' } }
            ]
          }
        ]
      },
      {
        id: 'card_table',
        type: 'card',
        name: '客户列表',
        props: { title: '数据列表', bordered: false },
        styles: { borderRadius: '8px', flex: 1 },
        children: [
          {
            id: 'container_actions',
            type: 'container',
            name: '操作区',
            props: {},
            styles: { display: 'flex', justifyContent: 'flex-end', border: 'none', padding: '0', marginBottom: '16px', backgroundColor: 'transparent' },
            children: [
              { id: 'btn_new', type: 'button', name: '新建客户', props: { text: '新建客户', type: 'primary' }, styles: { margin: '0' } }
            ]
          },
          {
            id: 'table_customers',
            type: 'table',
            name: '客户表格',
            props: {
              columns: [
                { title: '客户名称', dataIndex: 'name' },
                { title: '行业', dataIndex: 'industry' },
                { title: '负责人', dataIndex: 'owner' },
                { title: '状态', dataIndex: 'status' },
                { title: '更新时间', dataIndex: 'updatedAt' }
              ],
              dataSource: [
                { id: 1, name: '企鹅科技(深圳)有限公司', industry: '互联网', owner: '张三', status: '活跃', updatedAt: '2023-10-01 10:00' },
                { id: 2, name: '宇宙探索集团', industry: '航空航天', owner: '李四', status: '跟进中', updatedAt: '2023-10-02 14:30' },
                { id: 3, name: '绿洲农业发展', industry: '农业', owner: '王五', status: '已签约', updatedAt: '2023-10-03 09:15' }
              ],
              pagination: false,
              size: 'middle'
            },
            styles: {}
          }
        ]
      }
    ]
  });
});
</script>

<style scoped>
.mock-designer-page {
  width: 100vw;
  height: 100vh;
  overflow: hidden;
}
</style>
