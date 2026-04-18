import { describe, it, expect } from 'vitest';
import { groupDiffsByGroup } from '..';

describe('groupDiffsByGroup', () => {
  it('按顶层段分组', () => {
    const m = groupDiffsByGroup([
      { op: 'add', path: '/pages/0/root', after: 'X' },
      { op: 'replace', path: '/pages/1/path', before: '/a', after: '/b' },
      { op: 'remove', path: '/variables/foo' },
      { op: 'replace', path: '/displayName', before: 'Old', after: 'New' }
    ]);
    expect(Object.keys(m).sort()).toEqual(['_root', 'displayName', 'pages', 'variables'].sort().filter((k) => k !== '_root').concat(['displayName']).filter((v, i, a) => a.indexOf(v) === i).sort());
    expect(m['pages'].length).toBe(2);
    expect(m['variables'].length).toBe(1);
    expect(m['displayName'].length).toBe(1);
  });

  it('空 ops 返回空对象', () => {
    expect(groupDiffsByGroup([])).toEqual({});
  });

  it('根级 path 归到 _root', () => {
    const m = groupDiffsByGroup([{ op: 'replace', path: '/', before: 'a', after: 'b' }]);
    expect(m['_root']).toBeDefined();
  });
});
