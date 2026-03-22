import { ref } from 'vue';
import { message } from 'ant-design-vue';
import { i18n } from '@/i18n';
// import { requestApi } from '@/services/api-core';

export interface UseTableExportOptions {
  fileName?: string;
  auditResource?: string;
  auditAction?: string;
}

export function useTableExport(options: UseTableExportOptions = {}) {
  const isExporting = ref(false);

  /**
   * 等保审计日志前端防线
   */
  const recordAuditLog = async (action: string, resource: string, details: string) => {
    try {
      // TODO: 接驳后端的 /api/v1/audit/log 接口以确保等保日志完整性
      console.log(`[AUDIT Security Log] ${action} on ${resource}: ${details}`);
      // await requestApi('/audit/log', { method: 'POST', body: JSON.stringify({ action, resource, details }) });
    } catch (e) {
      console.error('Audit log failed, compliance warning', e);
    }
  };

  /**
   * 基于可见列的数据导出
   */
  const exportCsv = async (columns: any[], data: any[]) => {
    const { t } = i18n.global as any;
    if (!data || data.length === 0) {
      message.warning(t('tableUi.exportNoData'));
      return;
    }

    isExporting.value = true;
    try {
      // 1. 等保要求：大范围数据读取与导出操作强行触发审计
      await recordAuditLog(options.auditAction || 'EXPORT', options.auditResource || 'TableData', `Exported ${data.length} rows to CSV`);

      // 2. 导出过滤：根据当前列最新用户偏好可见状态
      const visibleColumns = columns.filter(c => c.visible !== false && c.dataIndex !== '_actions' && c.key !== 'actions');
      const headers = visibleColumns.map(c => c.title || c.key).join(',');
      
      const rows = data.map(item => {
        return visibleColumns.map(c => {
          const val = item[c.dataIndex || c.key];
          // 处理文本中包含英文逗号的情况
          return `"${String(val ?? '').replace(/"/g, '""')}"`;
        }).join(',');
      });

      const csvContent = [headers, ...rows].join('\n');
      const blob = new Blob(['\uFEFF' + csvContent], { type: 'text/csv;charset=utf-8;' });
      
      const link = document.createElement('a');
      link.href = URL.createObjectURL(blob);
      link.download = `${options.fileName || 'export'}.csv`;
      link.style.display = 'none';
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(link.href);
      
      message.success(t('tableUi.exportSuccess'));
    } catch (err: any) {
      const { t } = i18n.global as any;
      message.error(err.message || t('tableUi.exportFailed'));
    } finally {
      isExporting.value = false;
    }
  };

  return {
    isExporting,
    exportCsv
  };
}
