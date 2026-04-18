import { describe, it, expect, beforeEach } from 'vitest';
import type { ComponentMeta } from '@atlas/lowcode-schema';
import { registerComponent, getRegistry, getMeta, listMetas, __resetRegistryForTesting } from '..';
import { MetadataDrivenViolationError } from '../principles';

beforeEach(() => __resetRegistryForTesting());

const minMeta: ComponentMeta = {
  type: 'X',
  displayName: 'X',
  category: 'misc',
  version: '1.0.0',
  runtimeRenderer: ['web'],
  bindableProps: [],
  supportedEvents: [],
  childPolicy: { arity: 'none' }
};

describe('registry', () => {
  it('register / get / list', () => {
    registerComponent(minMeta);
    expect(getMeta('X')?.type).toBe('X');
    expect(listMetas().length).toBe(1);
    expect(getRegistry().size).toBe(1);
  });

  it('禁止覆盖', () => {
    registerComponent(minMeta);
    expect(() => registerComponent(minMeta)).toThrow();
  });

  it('元数据驱动校验拒绝 fetch', () => {
    expect(() => {
      registerComponent(
        { ...minMeta, type: 'BadFetch' },
        { implementationDescriptor: { importedGlobals: ['fetch'], importedPackages: [] } }
      );
    }).toThrow(MetadataDrivenViolationError);
  });

  it('元数据驱动校验拒绝 workflow_api 包', () => {
    expect(() => {
      registerComponent(
        { ...minMeta, type: 'BadWfApi' },
        {
          implementationDescriptor: {
            importedGlobals: [],
            importedPackages: ['@coze-arch/bot-api/workflow_api']
          }
        }
      );
    }).toThrow(MetadataDrivenViolationError);
  });

  it('合法实现描述符通过', () => {
    expect(() => {
      registerComponent(
        { ...minMeta, type: 'Good' },
        {
          implementationDescriptor: {
            importedGlobals: ['React.createElement'],
            importedPackages: ['@douyinfe/semi-ui']
          }
        }
      );
    }).not.toThrow();
  });
});
