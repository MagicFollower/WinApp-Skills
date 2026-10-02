# kill：向进程发送信号

> 给指定 PID 发信号，不带选项时默认 SIGTERM；先温和终止、再强制杀死是常规流程。

## 语法

```bash
kill [选项] <PID>...
kill -l [信号名或编号]
kill -9 2341 2342
kill -s HUP "$(pgrep -o nginx)"
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-l` | 列出信号名；`kill -l 9` 把编号译成名，`kill -l TERM` 反查编号 |
| `-L` | 以表格形式列出全部信号，便于对照编号与名称 |
| `-s <信号>` | 显式指定信号，可写名称或编号，如 `-s KILL`、`-s 9` |
| `-n <编号>` | 只按数字解释信号，跳过名称查找 |
| `-<信号>` | 传统简写，`-9`、`-HUP` 等价于 `-s` 写法 |
| `--` | 结束选项解析，要给负数（进程组号）发信号时必须先写它 |

### 常见信号（x86_64 / aarch64 Linux）

| 信号 | 编号 | 典型用途 |
| --- | --- | --- |
| `HUP` | 1 | 挂起或重载配置，nginx、rsyslog 用它 reload |
| `INT` | 2 | 等价 `Ctrl+C`，请求中断 |
| `QUIT` | 3 | 退出并写 core，Java 用它打印线程栈 |
| `KILL` | 9 | 强制杀死，进程无法捕获 |
| `TERM` | 15 | 默认的优雅终止 |
| `USR1` | 10 | 自定义，常见于日志重开、状态转储 |
| `USR2` | 12 | 自定义，常见于平滑升级 |
| `CONT` | 18 | 恢复被 STOP/TSTP 暂停的进程 |
| `STOP` | 19 | 暂停进程，无法捕获 |
| `TSTP` | 20 | 等价 `Ctrl+Z`，交互暂停 |

## 示例

标准两段式停止：先 TERM 让它清理，超时后补 KILL。
```bash
kill -TERM "$pid"
sleep 5
kill -0 "$pid" 2>/dev/null && kill -KILL "$pid"
```

重载 nginx 配置，主进程不动，只让 worker 换新。
```bash
kill -HUP "$(cat /var/run/nginx.pid)"
```

`kill -0` 不发信号，只判断进程存在且当前用户有权操作。
```bash
if kill -0 "$pid" 2>/dev/null; then echo running; fi
```

连同整棵子进程树一起停：先取 PGID，再对负数发信号。
```bash
kill -TERM -- "-$(ps -o pgid= -p "$pid" | tr -d ' ')"
```

## 注意事项

- 只能给自身 UID 的进程发信号，跨用户需要 root 或 `CAP_KILL`；先 `ps -o user= -p <PID>` 确认归属。
- `SIGKILL`、`SIGSTOP` 不可捕获或屏蔽，`-9` 会跳过临时文件与锁的释放；子进程被重新挂到 init 下，父进程不 `wait` 就成了僵尸。卡在 `D` 状态的进程连 `-9` 都收不到，只能等 IO 返回。
- systemd 托管的服务不要直接 `kill`：`Restart=` 会把它拉起来，看起来像杀不死，应改用 `systemctl stop`。
- bash 内建 `kill` 支持作业号（`kill %1`、`kill -TERM %%`），procps 的 `/bin/kill` 只认 PID；用 `type -a kill` 看当前生效的是哪个，`\kill` 可强制调用外部版本。

## 相关命令

- `pkill` — 按名字或命令行匹配后批量发信号
- `pgrep` — 只取 PID，配合命令替换
- `killall` — 按可执行文件名终止（psmisc 包）
