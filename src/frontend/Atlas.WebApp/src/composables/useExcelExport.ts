import { ref } from "vue";
import { message } from "ant-design-vue";
import { getAccessToken, getTenantId } from "@/utils/auth";
import { API_BASE, requestApi } from "@/services/api-core";
import type { ApiResponse } from "@/types/api";

/**
 * Excel 导出/导入 Composable
 * 封装文件下载（Blob）和上传逻辑，统一处理认证头
 */
export function useExcelExport() {
  const exporting = ref(false);
  const importing = ref(false);

  /**
   * 触发文件下载（通过 Blob URL）
   */
  async function downloadBlob(url: string, filename: string) {
    const token = getAccessToken();
    const tenantId = getTenantId();
    const baseUrl = API_BASE;

    const response = await fetch(`${baseUrl}${url}`, {
      headers: {
        Authorization: `Bearer ${token ?? ""}`,
        "X-Tenant-Id": tenantId ?? ""
      }
    });

    if (!response.ok) {
      throw new Error(`下载失败 (${response.status})`);
    }

    const blob = await response.blob();
    const objectUrl = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = objectUrl;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(objectUrl);
  }

  /**
   * 导出用户列表
   */
  async function exportUsers(keyword?: string) {
    exporting.value = true;
    try {
      const query = keyword ? `?keyword=${encodeURIComponent(keyword)}` : "";
      await downloadBlob(
        `/users/export${query}`,
        `users_${new Date().toISOString().slice(0, 10)}.xlsx`
      );
      message.success("导出成功");
    } catch (e: unknown) {
      message.error(e instanceof Error ? e.message : "导出失败");
    } finally {
      exporting.value = false;
    }
  }

  /**
   * 下载用户导入模板
   */
  async function downloadImportTemplate() {
    try {
      await downloadBlob("/users/import-template", "user_import_template.xlsx");
    } catch (e: unknown) {
      message.error(e instanceof Error ? e.message : "下载模板失败");
    }
  }

  /**
   * 上传并导入用户 Excel
   * @returns 导入结果 { totalRows, successCount, failureCount, errors }
   */
  async function importUsers(file: File): Promise<ImportResult | null> {
    importing.value = true;
    try {
      const formData = new FormData();
      formData.append("file", file);
      const response = await requestApi<ApiResponse<ImportResult>>("/users/import", {
        method: "POST",
        body: formData
      });
      return response.data ?? null;
    } catch (e: unknown) {
      message.error(e instanceof Error ? e.message : "导入失败");
      return null;
    } finally {
      importing.value = false;
    }
  }

  return { exporting, importing, exportUsers, downloadImportTemplate, importUsers };
}

export interface ImportError {
  row: number;
  field: string;
  message: string;
}

export interface ImportResult {
  totalRows: number;
  successCount: number;
  failureCount: number;
  errors: ImportError[];
}
