/**
 * 数据类组件实现（M06 P1-1，4 件）：WaterfallList / Table / List / Pagination
 */
import * as React from 'react';
import { List as SemiList, Pagination as SemiPagination, Table as SemiTable } from '@douyinfe/semi-ui';
import type { ComponentRenderer } from './runtime-types';

const WaterfallList: ComponentRenderer = ({ props, fireEvent, getContentParam }) => {
  const items = (Array.isArray(props.items) ? props.items : (getContentParam?.('data') as unknown[] | undefined) ?? []) as Array<Record<string, unknown>>;
  const columns = typeof props.columns === 'number' ? props.columns : 2;
  const buckets: Array<Array<Record<string, unknown>>> = Array.from({ length: columns }, () => []);
  items.forEach((it, i) => buckets[i % columns].push(it));
  const sentinelRef = React.useRef<HTMLDivElement | null>(null);
  React.useEffect(() => {
    const el = sentinelRef.current;
    if (!el) return;
    const obs = new IntersectionObserver((entries) => {
      if (entries.some((e) => e.isIntersecting)) fireEvent('onScrollEnd', null);
    });
    obs.observe(el);
    return () => obs.disconnect();
  }, [fireEvent]);
  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: `repeat(${columns}, 1fr)`, gap: 8 }}>
        {buckets.map((bucket, c) => (
          <div key={c} style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {bucket.map((it, i) => (
              <div
                key={i}
                onClick={() => fireEvent('onItemClick', { item: it, index: i })}
                style={{ cursor: 'pointer', padding: 8, border: '1px solid var(--semi-color-border)', borderRadius: 4 }}
              >
                {String((it as { title?: unknown })?.title ?? JSON.stringify(it))}
              </div>
            ))}
          </div>
        ))}
      </div>
      <div ref={sentinelRef} style={{ height: 1 }} />
    </div>
  );
};

const Table: ComponentRenderer = ({ props, fireEvent, getContentParam }) => {
  const dataSource = (Array.isArray(props.dataSource) ? props.dataSource : (getContentParam?.('data') as unknown[] | undefined) ?? []) as Array<Record<string, unknown>>;
  const columns = (Array.isArray(props.columns) ? props.columns : []) as Array<{ title: string; dataIndex: string; key?: string }>;
  return (
    <SemiTable
      dataSource={dataSource}
      columns={columns}
      pagination={
        props.pagination === false
          ? false
          : { pageSize: typeof props.pagination === 'object' && props.pagination !== null && 'pageSize' in (props.pagination as Record<string, unknown>)
              ? Number((props.pagination as Record<string, unknown>).pageSize)
              : 10 }
      }
      onChange={(args) => fireEvent('onChange', args as unknown)}
      onRow={(record) => ({ onClick: () => fireEvent('onItemClick', { item: record }) })}
    />
  );
};

const List: ComponentRenderer = ({ props, fireEvent, getContentParam }) => {
  const items = (Array.isArray(props.items) ? props.items : (getContentParam?.('data') as unknown[] | undefined) ?? []) as Array<Record<string, unknown>>;
  return (
    <SemiList
      dataSource={items}
      renderItem={(item, index) => (
        <SemiList.Item
          onClick={() => fireEvent('onItemClick', { item, index })}
          main={String((item as { title?: unknown })?.title ?? JSON.stringify(item))}
        />
      )}
    />
  );
};

const Pagination: ComponentRenderer = ({ props, fireEvent }) => (
  <SemiPagination
    currentPage={typeof props.current === 'number' ? props.current : 1}
    pageSize={typeof props.pageSize === 'number' ? props.pageSize : 10}
    total={typeof props.total === 'number' ? props.total : 0}
    onPageChange={(page) => fireEvent('onChange', { current: page })}
  />
);

export const DATA_COMPONENTS: Record<string, ComponentRenderer> = {
  WaterfallList,
  Table,
  List,
  Pagination
};
