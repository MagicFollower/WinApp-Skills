# systemctl：服务与单元管理

> 对 systemd 单元做启停、开机自启、状态查询与依赖检查，是管理服务进程的唯一推荐入口。

## 语法

```bash
systemctl [选项] <子命令> [单元...]
systemctl start nginx.service
systemctl enable --now docker.service
systemctl status nginx --no-pager
```

## 常用选项

| 子命令 | 说明 |
| --- | --- |
| `start` / `stop` / `restart` | 启动、停止、先停后起，默认阻塞到作业完成 |
| `reload` / `reload-or-restart` | 执行单元里的 `ExecReload=`（通常是发 SIGHUP）；未定义则报错或退化成重启 |
| `status` / `show` | 打印活动状态、主进程与最近日志；`show` 输出结构化属性 |
| `is-active` / `is-enabled` | 只回一个单词，退出码可判分支 |
| `enable` / `disable` | 建立或移除开机自启软链接，不改变当前运行状态 |
| `mask` / `unmask` | 链接到 `/dev/null`，连手动 `start` 都被拒绝 |
| `daemon-reload` | 单元文件被改动后重新读取 `/etc/systemd/system` 等目录 |
| `list-units` / `list-unit-files` | 前者列已加载单元（默认不显示 inactive），后者列单元文件与自启状态 |
| `list-dependencies` | 展示依赖树，配 `--reverse` 看谁依赖它 |
| `cat` / `edit` | 查看单元与 drop-in 内容 / 用编辑器生成分层覆盖 |
| `reset-failed` / `kill` | 清除 failed 标记与重启计数 / 给单元进程发信号 |

### 常用开关

| 开关 | 说明 |
| --- | --- |
| `--no-pager`, `-l` | 不分页、不截断，远程排障与截图时好用 |
| `--no-block` | 只提交作业不等待，避免卡住脚本 |
| `--wait` | 停止类命令等到单元彻底退出再返回 |
| `--now` | 与 `enable`/`disable` 连用，同时立即起或停 |
| `--runtime` | 改动只落在 `/run`，重启后自动消失 |
| `--user` | 操作当前用户的单元而非系统级 |
| `--type=service` / `--state=failed` | 过滤 `list-units` 的类型与状态 |
| `-p <属性>` / `--value` | `show` 只取指定属性，`--value` 只打印值便于赋值 |

## 示例

改完配置先重载单元定义，再重启并确认状态。
```bash
systemctl daemon-reload
systemctl restart nginx.service
systemctl status nginx.service --no-pager
```

新增自启并立刻起来，`--now` 省掉第二条命令。
```bash
systemctl enable --now chronyd.service
systemctl is-enabled chronyd.service
```

脚本里判断是否在运行，用退出码而不是解析文本。
```bash
systemctl is-active --quiet postgresql || systemctl start postgresql
```

## 注意事项

- 单元名可省后缀（`nginx` 等同 `nginx.service`），但模板实例必须完整写，如 `getty@tty1.service`；`static` 表示单元没有 `[Install]` 段，本来就无法 enable。
- `enable` 只写开机链接不会启动，`disable` 只删链接不会停止正在跑的进程；`Requires=` 是强依赖（被依赖方停止会牵连自己），`Wants=` 是弱依赖，而 `After=`/`Before=` 只管顺序不管存在性，两者要成对写。
- `reload` 依赖单元定义了 `ExecReload=`，缺省报 "Job type reload is not applicable"；单元进入 failed 后可能因启动频率限制拒绝再次 start，先 `reset-failed`。
- `systemctl edit` 之后新版 systemd 会自动重载，老版本仍需手动 `daemon-reload`；由 SysV 脚本生成的 `generated` 单元不支持 enable，建议改写成原生 unit。

## 相关命令

- `journalctl` — 查看单元的详细日志
- `ps` / `pgrep` — 核对 systemd 拉起的主进程与子进程
- `systemd-analyze` — 依赖图与启动耗时
