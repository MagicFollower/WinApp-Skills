# ps：进程状态快照

> 打印某一时刻的进程列表，不自动刷新，适合脚本采集、管道过滤与故障留档。

## 语法

```bash
ps [选项]
ps aux                       # BSD 风格：全部进程的用户导向详情
ps -ef                       # System V 风格：完整格式，含 PPID
ps -eo <列名串> --sort=<键>  # 自选列 + 排序
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-e`, `-A` | 选择全部进程 |
| `-a` | 选择终端上的进程（不含会话 leader） |
| `-x` | 选择无控制终端的进程，常与 `-a` 合成 `ax` |
| `-u <用户>` | 按用户名或 UID 筛选，逗号分隔可写多个 |
| `-f` | 完整格式：UID PID PPID C STIME TTY TIME CMD |
| `-o`, `--format <串>` | 自选列，如 `pid,ppid,user,etime,%cpu,args` |
| `--sort <键>` | 排序键，`-` 降序、`+` 升序，如 `--sort=-%cpu,pid` |
| `--ppid <PID>` | 只列出指定父进程的直接子进程 |
| `-p <PID>` | 按 PID 选择，可写 `ps -p 1,2,3` |
| `-C <命令名>` | 按可执行文件名选择 |
| `-L`, `-T` | 展开线程，附带 LWP 列 |
| `-w`, `-ww` | 加宽输出，`-ww` 不限宽、不截断命令行 |
| `--forest` | ASCII 树展示父子关系 |
| `--no-headers` | 不打印表头，便于 `awk`/`wc` 处理 |

## 示例

找出 CPU 占用最高的 10 个进程，负号代表降序，`head -11` 保留表头。
```bash
ps -eo pid,user,%cpu,%mem,etime,cmd --sort=-%cpu | head -11
```

排查主进程派生的 worker，用 `--ppid` 直接收口到子进程。
```bash
ps --ppid "$(pgrep -o -x gunicorn)" -o pid,user,etime,args
```

统计 web 用户进程数，用于会话数或容器进程数上限告警。
```bash
ps -u www-data --no-headers | wc -l
```

捞出 D 状态（不可中断 IO）与 Z 状态（僵尸），定位卡死的磁盘或 NFS 调用。
```bash
ps -eo pid,stat,etime,wchan:29,cmd | awk '$2 ~ /^[dDzZ]/'
```

压测前后各存一份快照，比较进程与启动时间变化。
```bash
ps -eo pid,ppid,user,start_time,cmd --sort=pid -ww > /tmp/ps.$(date +%H%M)
```

## 注意事项

- `ps` 只是瞬间快照，`%CPU` 是进程生命周期均值而非当前值；看实时波动用 `top`。
- `aux` 中的 `u` 是"用户导向格式"，单独的 `-u` 是"按用户筛选"，含义不同；混用两套风格时统一写 `ps -e -o ...` 更可控。
- STAT 列：`R` 运行、`S` 可中断睡眠、`D` 不可中断 IO、`T` 停止、`Z` 僵尸、`I` 内核空闲线程；后缀 `<` 高优先级、`N` 低优先级、`+` 属前台进程组、`s` 会话 leader、`l` 多线程。
- 列名与排序键随 procps-ng 版本有增减，落地前用 `ps --help all` 核对；管道或重定向时输出按 `COLUMNS` 截断，务必加 `-ww`。

## 相关命令

- `top` — 循环刷新的实时进程视图
- `pgrep` — 只输出 PID，方便命令替换
- `pkill` — 按名字或命令行批量发信号
- `pidstat` — 按间隔采样 CPU/IO 占用（sysstat 包）
