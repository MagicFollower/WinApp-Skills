# pkill：按名称或模式终止进程

> 用进程名或完整命令行做匹配并批量发信号，省掉先查 PID 再 kill 的两步。

## 语法

```bash
pkill [选项] <模式>
pkill --signal <信号> [选项] <模式>
pkill -HUP -x nginx
pkill -9 -f 'worker --name=cron'
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-f` | 匹配完整命令行；默认只比对 `comm`，最长 15 字符 |
| `-u <用户>` | 按有效 UID 筛选，用户名或数字，逗号分隔多个 |
| `-U <用户>` | 按真实 UID 筛选，不含 setuid 后的身份 |
| `-t <tty>` | 按控制终端筛选，不带 `/dev/`，如 `pts/3`；`?` 表示无终端 |
| `-g <PGID>` | 限定进程组，`-g 0` 为调用者所在组 |
| `-s <SID>` | 限定会话 ID |
| `-P <PPID>` | 只匹配指定父进程的子进程 |
| `-n` / `-o` | 只命中最新 / 最古老的那个进程 |
| `-x` | 要求完全相等，避免宽泛模式误伤 |
| `-v` | 反向匹配，命中不满足条件的进程 |
| `-e` | 打印被处理的 PID 与命令行，便于事后核对 |
| `--older <秒>` | 只处理运行时长超过指定秒数的进程 |
| `-<信号>` | 传统写法，`-9`、`-HUP`，默认 SIGTERM |

## 示例

先数一遍命中多少个，再决定发不发信号，是最省心的习惯。
```bash
pgrep -c -f 'pdf-server'
pkill -TERM -f 'pdf-server'
```

清理某个卡死登录会话里该用户的全部进程。
```bash
pkill -u dev -t pts/3
```

长期未响应才处理：只终止运行超过 2 小时的实例，新起的不动。
```bash
pkill -f --older 7200 --signal TERM 'backup.sh'
```

重载 nginx 配置，`-x` 保证只匹配进程名精确等于 nginx 的进程。
```bash
pkill -HUP -x nginx
```

自研 agent 被反复拉起、堆了几代，只处理最早的主进程。
```bash
pkill -o -f '/usr/local/bin/agent --config'
```

## 注意事项

- 模式是扩展正则（ERE）；不加 `-f` 时只比对 15 字符进程名，带路径带参数的目标必须用 `-f`。
- `pkill`/`pgrep` 永不匹配自身，但会匹配你的登录 shell 或其父进程：`pkill -f ssh`、`pkill -f bash` 极易把自己踢下线，动手前先 `pgrep -af <模式>` 确认。
- `-u <某用户>` 等于清空该用户全部进程（含当前会话），一般只在处理僵尸登录或批量登出时用，且需要 root。
- 退出码：命中并发出信号为 0，无命中为 1，脚本里写 `pkill foo || true` 忽略"本来就没在跑"。`-e`、`--older`、`-c` 是较新 procps-ng 才有的，老版本先 `pgrep` 再 `kill`。

## 相关命令

- `pgrep` — 相同匹配条件，只输出 PID 不杀进程
- `killall` — 按可执行文件名精确终止（psmisc 包）
- `ps` — 查看命中进程到底在做什么
- `systemctl` — 停由服务管理器拉起的进程
