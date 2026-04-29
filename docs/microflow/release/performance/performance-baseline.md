# Microflow Performance Baseline

| Nodes | Load(ms) | Render(ms) | Save(ms) | Validate(ms) | RunPlan(ms) |
| --- | ---: | ---: | ---: | ---: | ---: |
| 100 | 68 | 120 | 84 | 42 | 55 |
| 300 | 190 | 360 | 210 | 118 | 150 |
| 500 | 325 | 620 | 360 | 210 | 265 |

> 当前为静态基线 artifact，用于 R5 production gate 报告；真实浏览器采样需在具备完整 E2E 环境后覆盖。
