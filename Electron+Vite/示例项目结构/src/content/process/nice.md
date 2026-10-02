# nice / renice：调整 CPU 调度优先级

> `nice` 以指定优先级启动新程序，`renice` 修改已在运行的进程、进程组或某个用户的全部进程。

## 语法

```bash
nice [-n 调整值] 命令 [参数...]
renice [-n] 新优先级 [-u 用户... | -g 进程组... | -p PID...]
nice -n 10 make -j8
renice -n -5 -p 2341
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-n <调整值>` | `nice` 用相对调整值（`+5`、`-10`），旧式可省 `-n` 写 `nice -5 cmd` |
| `-n <优先级>` | `renice` 的是绝对值（`-20` 到 `19`），不是增量；`-n` 可省略 |
| `-p <PID...>` | 按进程号设置，可跟多个 PID |
| `-g <PGID...>` | 按进程组设置，一次覆盖整组进程 |
| `-u <用户...>` | 按用户名或 UID 设置，影响该用户当前所有进程 |
| `--help` / `-h` | 显示各自用法 |
| `--version` / `-V` | 显示版本 |
| 默认值 | 不调整时 nice 值为 0，`-20` 最激进、`19` 最谦让 |

查看某进程当前的设置值：`ps -o pid,ni,pri,cmd`。

## 示例

大版本编译不想拖慢线上服务，直接压到最低优先级。
```bash
nice -n 19 make -j"$(nproc)" > build.log 2>&1 &
```

后台批量压缩同样降权，并记录 PID 便于回查。
```bash
nice -n 15 tar -czf /backup/app.tar.gz /srv/app
ps -o pid,ni,cmd -p $!
```

误调高的关键进程要紧急改回 0 级，负值或改别人的进程需要 root。
```bash
sudo renice -n 0 -p "$(pgrep -o nginx)"
```

整机让路给在线业务：按用户把训练任务全部降权。
```bash
sudo renice -n 15 -u trainer
```

由 systemd 托管的服务把优先级写进单元 drop-in，而不是事后 renice。
```bash
systemctl edit cron        # 在 [Service] 段加 Nice=10
systemctl restart cron
systemctl show cron -p Nice
```

## 注意事项

- 普通用户只能把数值往大调（降低自己的优先级），调高后就调不回来；使用负值或改别人的进程需要 root（`CAP_SYS_NICE`）。受限环境下可下调的幅度由 `RLIMIT_NICE` 决定，用 `ulimit -e` 查看。
- nice 只管 CPU 时间片权重：磁盘 IO 用 `ionice`，实时调度类（`SCHED_FIFO`/`SCHED_RR`，见 `chrt`）完全绕过 nice；子进程继承父进程的 nice 值。
- 每级约 10% 的 CPU 份额变化是经验值而非精确比例；`top` 的 `NI` 列是设置值，`PRI` 是内核实时算出的动态值，别混读。
- 容器里若通过 cgroup 限制了 CPU 配额，调度先按配额切分，nice 的效果会被明显削弱；`renice 19 2341` 这类不带 `-p` 的老式写法多数实现仍按 PID 解释，脚本请统一加 `-p`。

## 相关命令

- `ps` — `-o pid,ni,pri` 核对当前 nice 值
- `top` — 交互键 `r` 即时 renice 某进程
- `ionice` / `chrt` — IO 优先级与实时调度策略
