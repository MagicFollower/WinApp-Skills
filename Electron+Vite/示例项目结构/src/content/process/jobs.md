# jobs / fg / bg：shell 作业控制

> 在同一个终端里挂起、恢复、前后切换作业，用作业号 `%1` 引用整条命令的进程组。

## 语法

```bash
jobs [-lnprst] [作业号...]
fg [作业号]
bg [作业号]
disown [-ar] [-h] [作业号...]
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `%1`, `%%`, `%+` | 按编号引用作业；`%%`/`%+` 是当前作业，`%-` 是下一个 |
| `%字符串`, `%?字符串` | 按命令名或命令行内容匹配作业 |
| `jobs -l` | 在状态行上追加 PID，管道作业会列出每个成员的 PID |
| `jobs -p` | 只输出进程号（bash 给作业内最近一个成员的 PID，POSIX sh 给进程组号） |
| `jobs -r` / `jobs -s` | 只显示正在运行 / 已停止的作业 |
| `jobs -n` | 只显示自上次通知后状态变化的作业 |
| `fg %n` | 取回前台，重新拿到键盘输入与信号（`Ctrl+C` 会打到它） |
| `bg %n` | 向作业发 SIGCONT，让它在后台继续跑 |
| `disown %n` | 从 shell 作业表删除，之后无法再用 `%n` 引用 |
| `disown -h %n` | 保留在作业表，但退出时不再给它发 SIGHUP |
| `disown -a` / `-r` | 作用于全部作业 / 仅正在运行的作业 |
| `kill -TERM %1` | 内建 `kill` 认作业号，按该作业的进程组发送信号 |

## 示例

编译占住终端时按 `Ctrl+Z` 挂起，`jobs` 里状态显示 `Stopped`。
```bash
make -j8
^Z
jobs -l
```

紧急命令跑完后，把编译取回前台继续看输出。
```bash
fg %1
```

要断开 SSH 又不打断任务：先后台化，再标记为不受退出挂断影响。
```bash
bg %1
disown -h %1
```

两种收尾方式：内建 `kill` 认作业号；跨会话或想连子进程一起停时改用 PGID 对整组发送。
```bash
kill -TERM %1
kill -TERM -- "-$(ps -o pgid= -p "$pid" | tr -d ' ')"
```

## 注意事项

- 作业表是每个 shell 进程私有的：换个 SSH 会话或从别的终端都无法 `fg`/`bg`，只能用 `pgrep` 找到 PID 后 `kill -CONT`。
- `Stopped`（STAT 为 `T`）的进程不会执行，`disown` 也不会替它发 SIGCONT；离开终端前务必 `bg` 或 `kill -CONT`，否则任务永远卡在那里。
- 后台作业读终端输入会收到 SIGTTIN、写终端输出会收到 SIGTTOU 而被再次挂起，这类程序必须重定向 stdin/stdout（配 `nohup` 或 `setsid`）才能真正后台跑。
- 脚本里默认关闭 monitor 模式，没有独立进程组；需要作业时先 `set -m`，收尾用 `wait` 或 `wait -n`（bash 4.3+）。`Ctrl+Z` 是 SIGTSTP，可被程序捕获或忽略，某些全屏程序需要自己的退出键。

## 相关命令

- `kill` — 用作业号或 PID 精确发送 CONT/TERM
- `nohup` — 忽略 SIGHUP，配合 `&` 跑长任务
- `ps` — 用 `stat`、`pgid`、`tty` 列核对作业真实状态
