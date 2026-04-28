import React, { useState } from 'react';
import { useQuery, useMutation } from '@tanstack/react-query';
import { SideSheet, Input, List, Typography, Spin, Empty, Tag, Space } from '@douyinfe/semi-ui';
import type { FaqEntry } from '../services/api-core';
import { t } from '../i18n';
import { useLowcodeStudioHost } from '../host';

/**
 * UI Builder FAQ 抽屉（M14 + 二轮深审 #26）。
 *
 * - GET /api/v1/lowcode/faq?keyword=  关键字检索
 * - POST /api/v1/lowcode/faq/{id}/hit  命中后命中数 +1（用于热度排序）
 * - 命中后展开正文（Markdown 暂以 pre 简单展示，避免引入额外 markdown 解析依赖）
 */
export const FaqDrawer: React.FC<{ visible: boolean; onClose: () => void }> = ({ visible, onClose }) => {
  const [keyword, setKeyword] = useState('');
  const [opened, setOpened] = useState<FaqEntry | null>(null);
  const { api } = useLowcodeStudioHost();

  const faqQuery = useQuery({
    queryKey: ['lowcode-faq', keyword],
    queryFn: () => api.faq.search(keyword || undefined, 1, 50),
    enabled: visible
  });

  const hitMut = useMutation({
    mutationFn: (id: string) => api.faq.hit(id),
    onSuccess: (data) => {
      if (data) setOpened(data);
      faqQuery.refetch();
    }
  });

  return (
    <SideSheet title={t('lowcode_studio.faq.title')} visible={visible} onCancel={onClose} placement="right" size="medium">
      <Input
        prefix="🔍"
        placeholder={t('lowcode_studio.faq.search')}
        value={keyword}
        onChange={setKeyword}
        style={{ marginBottom: 12 }}
      />
      {faqQuery.isLoading ? <Spin /> : (
        <List
          dataSource={faqQuery.data ?? []}
          emptyContent={<Empty title="暂无 FAQ" />}
          renderItem={(e) => (
            <List.Item
              style={{ cursor: 'pointer', background: opened?.id === e.id ? '#e6f4ff' : undefined }}
              onClick={() => hitMut.mutate(e.id)}
              extra={
                <Space>
                  <Tag size="small" color="grey">命中 {e.hits}</Tag>
                  {e.tags && <Tag size="small">{e.tags.split(',')[0]}</Tag>}
                </Space>
              }
            >
              <Typography.Text strong>{e.title}</Typography.Text>
            </List.Item>
          )}
        />
      )}

      {opened && (
        <div style={{ marginTop: 16, padding: 12, background: '#fafafa', borderRadius: 4 }}>
          <Typography.Title heading={6} style={{ margin: '0 0 8px' }}>{opened.title}</Typography.Title>
          <pre style={{ whiteSpace: 'pre-wrap', fontSize: 12, lineHeight: 1.6, margin: 0 }}>{opened.body}</pre>
          <Typography.Paragraph type="tertiary" style={{ marginTop: 8, fontSize: 11 }}>
            最后更新 {new Date(opened.updatedAt).toLocaleString()}
          </Typography.Paragraph>
        </div>
      )}
    </SideSheet>
  );
};
