import React, { useState } from 'react';
import { useQuery, useMutation } from '@tanstack/react-query';
import { Button, Typography, Space, Form, Tabs, TabPane, Checkbox, Select, Toast, Banner, Layout } from '@douyinfe/semi-ui';
import { IconChevronLeft, IconLink, IconMessage, IconCode, IconGlobe, IconSetting, IconMore } from '@douyinfe/semi-icons';
import { t } from '../i18n';
import { useLowcodeStudioHost } from '../host';
import type { PublishArtifact } from '../services/api-core';

const { Header, Content } = Layout;

export const PublishPage: React.FC<{ appId: string; onBack?: () => void }> = ({ appId, onBack }) => {
  const { api, publishApi } = useLowcodeStudioHost();
  const [activeTab, setActiveTab] = useState('api');
  const [showBanner, setShowBanner] = useState(true);

  const previewQuery = useQuery({
    queryKey: ['project-ide-publish-preview', appId],
    queryFn: () => publishApi?.getPreview(appId),
    enabled: Boolean(publishApi)
  });

  const validationQuery = useQuery({
    queryKey: ['project-ide-validation', appId],
    queryFn: () => host?.validationApi?.validate(appId),
    enabled: Boolean(host?.validationApi)
  });

  const publishMut = useMutation({
    mutationFn: (vals: any) => {
      const matrix = { web: true }; // Simplified for mock
      if (publishApi) {
        return publishApi.publish(appId, {
          kind: 'hosted',
          versionLabel: vals.version,
          rendererMatrixJson: JSON.stringify(matrix)
        });
      }
      return api.publish.publish(appId, 'hosted', { rendererMatrixJson: JSON.stringify(matrix) });
    },
    onSuccess: () => {
      Toast.success('发布成功');
      onBack?.();
    },
    onError: (e: Error) => Toast.error(e.message)
  });

  const onSubmit = (values: any) => {
    publishMut.mutate(values);
  };

  return (
    <Layout style={{ height: '100vh', backgroundColor: '#f2f3f5' }}>
      {/* Top Header */}
      <Header style={{ height: 60, display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0 24px', backgroundColor: '#fff', borderBottom: '1px solid var(--semi-color-border)' }}>
        <Space>
          <Button icon={<IconChevronLeft />} theme="borderless" onClick={onBack} />
          <Typography.Title heading={5} style={{ margin: 0 }}>发布</Typography.Title>
        </Space>
        <Button type="primary" theme="solid" loading={publishMut.isPending} onClick={() => document.getElementById('publish-submit-btn')?.click()}>发布</Button>
      </Header>

      <Content style={{ overflow: 'auto' }}>
        {showBanner && (
          <Banner
            type="info"
            description="如果应用没有经过完整的试运行，可能发布结果不合预期。建议经过完整的试运行后再进行发布。"
            closeIcon
            onClose={() => setShowBanner(false)}
            style={{ justifyContent: 'center' }}
          />
        )}
        
        {previewQuery.data?.warnings && previewQuery.data.warnings.length > 0 && (
          <div style={{ maxWidth: 840, margin: '24px auto 0 auto' }}>
            {previewQuery.data.warnings.map((w, idx) => (
              <Banner key={idx} type="warning" description={w} style={{ marginBottom: 8, borderRadius: 8 }} closeIcon={null} />
            ))}
            {validationQuery.data?.issues?.filter((i: any) => i.code === 'obsolete_variable_reference' || i.Code === 'obsolete_variable_reference').map((issue: any, idx) => (
              <Banner 
                key={`val-${idx}`} 
                type="warning" 
                title="存在变量旧引用风险"
                description={
                  <Space vertical align="start" spacing={4}>
                    <Typography.Text>{issue.message || issue.Message}</Typography.Text>
                    <Typography.Text type="tertiary" style={{ fontSize: 12 }}>
                      建议在工作流 {issue.workflowId || issue.WorkflowId} 的节点 {issue.nodeId || issue.NodeId} 中将 {issue.expression || issue.Expression} 替换为 {issue.replacementSuggestion || issue.ReplacementSuggestion}
                    </Typography.Text>
                  </Space>
                }
                style={{ marginBottom: 8, borderRadius: 8 }} 
                closeIcon={null} 
              />
            ))}
          </div>
        )}

        <div style={{ maxWidth: 840, margin: '24px auto', paddingBottom: 60 }}>
          <Form id="publish-form" initValues={{ version: 'v0.0.1' }} onSubmit={onSubmit}>
            {/* Version Info Card */}
            <div style={{ backgroundColor: '#fff', borderRadius: 8, padding: '24px', marginBottom: 24, border: '1px solid var(--semi-color-border)' }}>
              <Typography.Title heading={5} style={{ marginBottom: 20 }}>版本信息</Typography.Title>
              <div style={{ display: 'flex', gap: 24 }}>
                <Form.Input field="version" label="版本号" style={{ width: 300 }} rules={[{ required: true }]} />
                <Form.TextArea field="description" label="版本描述" style={{ flex: 1 }} placeholder="请输入版本描述" maxCount={800} />
              </div>
            </div>

            {/* Publish Channels Card */}
            <div style={{ backgroundColor: '#fff', borderRadius: 8, padding: '24px', border: '1px solid var(--semi-color-border)' }}>
              <Typography.Title heading={5} style={{ marginBottom: 12 }}>选择发布渠道</Typography.Title>
              <Typography.Text type="tertiary" style={{ display: 'block', marginBottom: 24, fontSize: 12 }}>
                在以下平台发布你的应用，即表示你已充分理解并同意遵循各个发布渠道服务条款（包括但不限于任何隐私政策、社区指南、数据处理协议等）。
              </Typography.Text>

              <Tabs type="card" activeKey={activeTab} onChange={setActiveTab} style={{ marginBottom: 24 }}>
                <TabPane tab="API 或 SDK" itemKey="api" />
                <TabPane tab="小程序" itemKey="miniprogram" />
                <TabPane tab="社交平台" itemKey="social" />
                <TabPane tab="扣子" itemKey="coze" />
                <TabPane tab="MCP服务" itemKey="mcp" />
              </Tabs>

              {activeTab === 'api' && (
                <div>
                  <Typography.Text strong style={{ display: 'block', marginBottom: 16 }}>API 或 SDK</Typography.Text>
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                    {/* API Card */}
                    <div style={{ border: '1px solid var(--semi-color-border)', borderRadius: 8, padding: 16 }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 8 }}>
                        <Space>
                          <div style={{ width: 32, height: 32, borderRadius: 6, backgroundColor: '#eef2ff', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#4f46e5' }}>
                            <IconLink size="large" />
                          </div>
                          <Typography.Text strong>API</Typography.Text>
                        </Space>
                        <Checkbox value="api" />
                      </div>
                      <Typography.Text type="tertiary" style={{ fontSize: 12, display: 'block' }}>
                        仅创建以工作流的应用支持该发布方式；在发布前请先创建一个令牌。
                      </Typography.Text>
                    </div>

                    {/* Chat SDK Card */}
                    <div style={{ border: '1px solid var(--semi-color-border)', borderRadius: 8, padding: 16 }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 8 }}>
                        <Space>
                          <div style={{ width: 32, height: 32, borderRadius: 6, backgroundColor: '#eff6ff', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#2563eb' }}>
                            <IconMessage size="large" />
                          </div>
                          <Typography.Text strong>Chat SDK</Typography.Text>
                        </Space>
                        <Checkbox value="chat-sdk" />
                      </div>
                      <Typography.Text type="tertiary" style={{ fontSize: 12, display: 'block', marginBottom: 16 }}>
                        将项目发布到Chat SDK。仅创建以对话流的项目支持该发布方式，安装方式请查看安装指引。
                      </Typography.Text>
                      <Select defaultValue="chatflow" style={{ width: '100%' }}>
                        <Select.Option value="chatflow">访客流 Chatflow</Select.Option>
                      </Select>
                    </div>

                    {/* Web SDK Card */}
                    <div style={{ border: '1px solid var(--semi-color-border)', borderRadius: 8, padding: 16 }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 8 }}>
                        <Space>
                          <div style={{ width: 32, height: 32, borderRadius: 6, backgroundColor: '#f0fdf4', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#16a34a' }}>
                            <IconCode size="large" />
                          </div>
                          <Typography.Text strong>Web SDK</Typography.Text>
                        </Space>
                        <Checkbox value="web-sdk" />
                      </div>
                      <Typography.Text type="tertiary" style={{ fontSize: 12, display: 'block' }}>
                        将项目中的前端应用到Web SDK。安装方式请查看安装指引。
                      </Typography.Text>
                    </div>
                  </div>
                </div>
              )}

              {activeTab === 'miniprogram' && (
                <div>
                  <Typography.Text strong style={{ display: 'block', marginBottom: 16 }}>小程序</Typography.Text>
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                    {/* 微信小程序 */}
                    <div style={{ border: '1px solid var(--semi-color-border)', borderRadius: 8, padding: 16 }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 8 }}>
                        <Space>
                          <div style={{ width: 32, height: 32, borderRadius: 6, backgroundColor: '#f0fdf4', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#16a34a' }}>
                            <IconMessage size="large" />
                          </div>
                          <Typography.Text strong>微信小程序 <Typography.Text type="tertiary" style={{ fontSize: 12, fontWeight: 'normal' }}>未配置</Typography.Text></Typography.Text>
                        </Space>
                        <Checkbox value="wx-mini" disabled />
                      </div>
                      <Typography.Text type="tertiary" style={{ fontSize: 12, display: 'block', marginBottom: 16 }}>
                        支持将一键发布至小程序
                      </Typography.Text>
                      <Button size="small">配置</Button>
                    </div>

                    {/* 抖音小程序 */}
                    <div style={{ border: '1px solid var(--semi-color-border)', borderRadius: 8, padding: 16 }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 8 }}>
                        <Space>
                          <div style={{ width: 32, height: 32, borderRadius: 6, backgroundColor: '#111', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#fff' }}>
                            <IconMessage size="large" />
                          </div>
                          <Typography.Text strong>抖音小程序 <Typography.Text type="tertiary" style={{ fontSize: 12, fontWeight: 'normal' }}>未配置</Typography.Text></Typography.Text>
                        </Space>
                        <Checkbox value="dy-mini" disabled />
                      </div>
                      <Typography.Text type="tertiary" style={{ fontSize: 12, display: 'block', marginBottom: 16 }}>
                        支持将一键发布至小程序
                      </Typography.Text>
                      <Button size="small">配置</Button>
                    </div>
                  </div>
                </div>
              )}
            </div>
            
            <button id="publish-submit-btn" type="submit" style={{ display: 'none' }} />
          </Form>
        </div>
      </Content>
    </Layout>
  );
};
