# pgrep：按条件查找进程 PID

> 用进程名或完整命令行做正则匹配，默认只输出 PID，可直接喂给 kill 或脚本变量。

## 语法

```bash
pgrep [选项] <模式>
pgrep -f '<完整命令行片段>'
kill -HUP "$(pgrep -o -x nginx)"
PID=$(pgrep -u root -t pts/0 sshd)
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-f` | 匹配完整命令行；默认只比对 `comm`，最长 15 字符 |
| `-u <用户>` | 按有效 UID/用户名筛选，逗号分隔可写多个 |
| `-U <用户>` | 按真实 UID 筛选 |
| `-t <tty>` | 按控制终端筛选，不带 `/dev/`；`?` 表示无终端 |
| `-g <PGID>` / `-s <SID>` | 限定进程组 / 会话 ID |
| `-P <PPID>` | 只列出指定父进程的子进程 |
| `-l` | 同时输出进程名，形如 `1234 sshd` |
| `-a` | 同时输出完整命令行，肉眼看命中了什么 |
| `-c` | 只输出命中的数量 |
| `-d <分隔符>` | 自定义 PID 之间的分隔符，如 `-d ','` |
| `-n` / `-o` | 只取最新启动 / 最古老的那个进程 |
| `-x` | 要求精确相等，不做部分匹配 |
| `-v` | 反向匹配，排除符合模式的进程 |
| `--older <秒>` | 只选运行时长超过指定秒数的进程 |

## 示例

判断服务是否在跑，用退出码做分支（命中为 0，未命中为 1）。
```bash
pgrep -x chronyd >/dev/null && echo "时间同步正常"
```

拿到主进程 PID 后重载配置，避免 `ps | awk` 这类脆弱写法。
```bash
kill -HUP "$(pgrep -o -x nginx)"
```

按命令行片段精确定位某个 Python 服务，用 `-a` 核对完整命令行。
```bash
pgrep -af 'python3 /srv/order/app.py'
```

统计当前 sshd 会话数，用于登录风暴告警。
```bash
pgrep -c sshd
```

把命中的 PID 拼成一行交给别的程序，例如一次看多个进程的内存映射。
```bash
pmap $(pgrep -d ' ' -u www-data php-fpm)
```

## 注意事项

- 短名匹配受 15 字符截断影响：`pgrep postgres-exporter` 可能一个都不中，需要 `-f` 或缩短模式；模式按 ERE 解释，点号、星号是正则元字符。
- 不会匹配自身，但会匹配调用它的 shell；`-f` 写宽泛关键词时先加 `-a` 或 `-c` 预检查，确认无误再配 `pkill`。
- 普通用户能把别人的进程查出来，但随后 `kill` 仍需相应权限；容器里 PID 命名空间不同，宿主机上看到的 PID 与容器内不一致。
- `-c`、`-a`、`--older` 属较新 procps-ng 特性，旧发行版（procps 3.x）可能缺项，报 unknown option 时退回 `ps -eo pid,args | grep`。

## 相关命令

- `pkill` — 相同匹配条件下直接发信号
- `ps` — 查看命中进程的完整字段
- `kill` — 对 pgrep 得到的 PID 精确操作
- `pidof` — 按可执行文件名输出全部 PID，空格分隔
