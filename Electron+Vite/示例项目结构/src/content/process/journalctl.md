# journalctl：查询 systemd 日志

> 按单元、时间窗口、优先级过滤 journald 收集的全部日志，支持跟随与跨启动回溯。

## 语法

```bash
journalctl [选项] [FIELD=值 ...]
journalctl -u nginx.service -f
journalctl --since "2026-10-02 09:00:00" --until "09:30" -p err
journalctl _PID=2341 -o verbose
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-u`, `--unit <单元>` | 只看指定单元（service/socket/mount 等），可重复给多个 |
| `-f`, `--follow` | 持续输出新条目，类似 `tail -f` |
| `--since`, `--until <时间>` | 时间窗口，接受 `YYYY-MM-DD HH:MM:SS`、`HH:MM`、`today`、`yesterday`，带空格要加引号 |
| `-p`, `--priority <级别>` | 0-7 或名称：`emerg` `alert` `crit` `err` `warning` `notice` `info` `debug`，`-p 3` 等同 `-p err` |
| `-b`, `--boot [ID]` | 限定某次启动：`-b` 或 `-b 0` 当前，`-b -1` 上一次 |
| `-n`, `--lines <N>` | 只看最后 N 条，`-n 0` 配合 `-f` 从空开始 |
| `-r`, `--reverse` | 倒序输出，最新在最上面 |
| `-o`, `--output <格式>` | `short` `short-iso` `short-precise` `json-pretty` `cat` `verbose` `export` |
| `-k`, `--dmesg` | 只看内核消息，隐含 `-b` |
| `-t`, `--identifier <名>` | 按 `SYSLOG_IDENTIFIER` 过滤，如 `-t sshd` |
| `-g`, `--grep <正则>` | 对消息正文做正则筛选（需带 PCRE2 构建） |
| `-m`, `--merge` / `-a` | 合并远程收到的日志 / 不截断长字段 |
| `--disk-usage` | 显示当前与归档 journal 占用的空间 |
| `--vacuum-size`, `--vacuum-time` | 主动瘦身：按体积（`500M`）或按时长（`2 weeks`） |
| `--rotate`, `--flush`, `--no-pager` | 切分文件 / 运行时日志落持久盘 / 不分页输出 |

## 示例

服务刚重启，先看最后 200 行再持续跟随，另开终端复现问题。
```bash
journalctl -u nginx.service -n 200 -f
```

只取故障时间窗内 warning 以上的内容，秒级时间要加引号。
```bash
journalctl -u order-api --since "2026-10-02 03:10:00" --until "2026-10-02 03:40:00" -p warning
```

主机无故重启后回溯上一次开机的错误，前提启用了持久化日志。
```bash
journalctl -b -1 -p err --no-pager
```

排查 OOM killer：内核消息配关键词，找到被杀的 PID。相对时间交给 `date` 换算最稳妥。
```bash
journalctl -k --since "$(date -d '2 hours ago' '+%F %T')" | grep -iE 'oom|killed process'
```

journal 撑爆分区时先量一下，再按体积和时长清理。
```bash
journalctl --disk-usage
journalctl --vacuum-size=300M --vacuum-time=14d
```

## 注意事项

- 读取他人或全部日志通常需要 root，或属于 `systemd-journal`（部分发行版是 `adm`/`wheel`）组；容器内没有 journald，会直接报 No journal files were found。
- `-b -1` 与 `--vacuum-*` 只在 `Storage=persistent`（日志写 `/var/log/journal`）时有历史可查，纯 `/run/log/journal` 重启即丢；实际保留量还受 `SystemMaxUse=` 约束。
- 同一字段多个条件是"或"，不同字段之间是"与"：`-u a -u b` 取并集，`-u a _PID=123` 取交集；服务由 `Type=forking` 派生的子进程日志可能落在别的 `_SYSTEMD_UNIT` 下。
- `--grep` 需要 systemd 编译时带 PCRE2，老版本用管道 `| grep` 兜底但会丢掉结构化字段。

## 相关命令

- `systemctl` — 查看单元状态与最近日志行
- `ps` / `pgrep` — 拿到 PID 后用 `_PID=` 反查日志
- `dmesg` — 直接读内核环形缓冲区
