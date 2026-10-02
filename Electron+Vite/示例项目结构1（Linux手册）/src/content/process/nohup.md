# nohup：忽略挂断信号后台运行

> 让命令忽略 SIGHUP，关掉终端或 SSH 断线后仍能跑完，输出默认落到 nohup.out。

## 语法

```bash
nohup 命令 [参数...]
nohup long-task.sh &
nohup long-task.sh > task.log 2>&1 < /dev/null &
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `--help` | 显示 coreutils 版 nohup 用法 |
| `--version` | 显示版本信息 |
| `-`, `--` | 结束选项解析，之后全部当作命令与参数 |
| 无其它开关 | nohup 只做一件事：把 SIGHUP 设为忽略，其余行为由重定向决定 |
| 输出缺省 | stdout 是终端时追加写 `./nohup.out`，不可写则退回 `$HOME/nohup.out` |

### 配套写法

| 写法 | 说明 |
| --- | --- |
| `&` | 交给 shell 后台执行，否则终端被一直占住 |
| `> file.log` | 显式指定日志文件；不带则走 `nohup.out` |
| `2>&1` | 错误输出并入同一份日志 |
| `< /dev/null` | 断开标准输入，避免脚本读 tty 时卡住 |
| `disown` | 从作业表移除，配合 nohup 更彻底 |
| `setsid` | 新建会话、真正脱离控制终端 |

## 示例

跑数小时的备份，显式重定向并按日期归档日志。
```bash
nohup bash /ops/backup.sh > /var/log/backup.$(date +%F).log 2>&1 < /dev/null &
```

启动后立刻记下 PID，方便后续精确 `kill -TERM`。
```bash
nohup python3 /srv/report.py > report.log 2>&1 &
echo $! > /tmp/report.pid
```

确认它确实还活着，并且已不属于当前终端（TTY 显示 `?`）。
```bash
ps -o pid,ppid,pgid,sid,tty,stat,cmd -p "$(cat /tmp/report.pid)"
```

输出文件可能被追加写，用 `tail -F` 跟踪进度。
```bash
tail -F /var/log/backup.$(date +%F).log
```

## 注意事项

- nohup 只挡 SIGHUP，对 `SIGTERM`、`SIGINT`、`SIGKILL` 无效；机器重启或别人 `pkill` 照样停，需要开机自启请写 systemd unit。
- 它不创建新会话、不脱离控制终端，只是改信号处理：只有终端关闭或 shell 设了 `huponexit` 时才依赖它，要彻底脱钩用 `setsid` 或 tmux/screen。
- `nohup.out` 以追加方式写入，反复执行会把日志越堆越长；两处默认位置都不可写时 nohup 直接报错退出。
- 程序自己重新注册了 SIGHUP 处理（部分守护进程会这么做）时 nohup 失效；tcsh 等 shell 的内建 `nohup` 与 coreutils 版本行为略有差异，跨 shell 部署统一写显式重定向 + `&` + `disown`。

## 相关命令

- `jobs` / `fg` / `bg` — 同一终端内的作业控制
- `setsid` — 新建会话并彻底脱离控制终端
- `systemd-run` — 把一次性任务交给 systemd 管理
