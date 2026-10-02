# 仿 macOS 桌面（示例工程）

一个用 Vite + React + TypeScript + Electron 搭的**仿 macOS 桌面**示例：无边框窗口铺满工作区，
页内自己画菜单栏、Dock、应用窗口、Spotlight 与启动台，并接上 100themes 的 24 套色板。
定位是"能跑、能改、能打包"的参考工程，不是产品。

上手与打包命令逐条带判据，见 [`doc/运行与构建（T0Level）.md`](./doc/运行与构建（T0Level）.md)。

## 屏上有什么

| 部件 | 行为 | 是不是真的 |
| --- | --- | --- |
| 菜单栏 | 苹果菜单、当前应用菜单、文件/显示/窗口/帮助、右侧搜索与控制中心、时钟 | 真：菜单项驱动窗口与设置 |
| Dock | reactbits 的鼠标放大动画、运行指示点、点击弹跳 | 真：磁贴就是应用启动器 |
| 应用窗口 | 流量灯（关/最小化/缩放）、拖拽标题栏、右下角拖拽改尺寸、双击标题栏缩放 | 真：页内窗口管理器 |
| 访达 | 列目录、进目录、后退、上一级、显示隐藏文件、文本预览 | 真：读的是本机主目录 |
| 备忘录 | 列表 + 编辑，写/读/删 | 真：落到 `userData/notes` |
| 计算器 | 四则、正负、百分号 | 真：支持键盘输入 |
| 外观 | 明暗三态、5 个变体、24 套色板、5 张壁纸 | 真：改的是持久化设置 |
| 关于本机 | Electron / Chromium / Node / 系统版本与目录位置 | 真：页内文本，不弹模态框 |
| Spotlight | 搜应用、色板、壁纸、文件夹、备忘录 | 真：回车即启动 |
| 启动台 | 全部应用网格 + 过滤 | 真 |
| Wi-Fi / 电池图标 | 只有形状 | **示意**：沙箱里读不到，也不联网 |
| 睡眠 / 重新启动 / 关机 | 菜单里是灰的 | **示意**：本工程不控制电源 |

## 快捷键

- `Ctrl+空格`（或 `Win+空格`）：聚焦搜索
- `F4`：启动台
- `Esc`：关掉搜索、启动台、右键菜单
- 计算器窗口打开时：数字、`+ - * /`、`Enter`、`Esc`
- 备忘录：`Ctrl+S` 立即保存（停止输入 700ms 也会自动存）

## 目录

```
index.html  package.json  tsconfig.json  vite.config.mts  electron-builder.yml
src/                 渲染层：桌面壳 components/、三个应用 apps/、样式 styles/、lib/、data/
electron/            主进程 main.ts + preload.ts + tsconfig.json + icon.ico
bin/                 脚本：dev/build 的 mjs 入口、打包与自证的 ps1、gen-themes.mjs
100themes/           色板种子：raw/ 500 份 colors.toml + manifest.json + preset.json（本工程白名单）
doc/                 运行与构建（T0Level）.md、README、SKILL.md（所依据的 Skill 快照）
dist/ dist-electron/ release/   产物
```

## 三个口径决定

1. **桌面壳是"一个无边框窗口 + 页内窗口"**，不是多个原生窗口。理由：macOS 桌面的观感来自
   整屏壁纸 + 悬浮窗口 + 毛玻璃，用原生多窗口在 Windows 上做不到无边框阴影与统一毛玻璃，
   而且窗口间动画（最小化飞进 Dock）跨进程做不了。
2. **明暗由色板变体决定**：`变体=自动` 时跟随 `scheme`（跟随系统/亮/暗），
   选了 `day`/`day-high-contrast` 这类亮色变体会把 `nativeTheme.themeSource` 一起翻成 `light`，
   这样滚动条、表单控件和内容不会一半深一半浅。
3. **色板只挂 24 套**（`100themes/preset.json`），但 `raw/` 保留 500 份全量种子；
   换清单重跑 `npm run themes` 即可，不必联网。

## 设计来源

- 动画组件：[reactbits](https://www.reactbits.dev) 的 Dock（鼠标磁贴放大）与 AnimatedList（进出视口的缩放列表），
  两份都按本工程的 token 契约重写了样式，源码顶部标了 fork 出处。
- 颜色：[bjarneo/100-themes](https://github.com/bjarneo/100-themes)，离线种子与生成器见 `100themes/README.md`。
- 工程骨架与打包口径：Skill `windows-electron-scaffold`（快照在 `doc/SKILL.md`）。
