# ss — 套接字与端口占用查询

> iproute2 的套接字统计工具，通过 netlink 直接读内核状态，比 netstat 更快、字段更全。

## 语法

```bash
ss [ OPTIONS ] [ FILTER ]
FILTER := [ -|state ] TCP-STATE | ADDRESS | PORT | 'sport = :8080'
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-t`, `-u`, `-w`, `-x` | 分别查看 TCP / UDP / RAW / Unix domain 套接字 |
| `-a`, `--all` | 同时列出监听与非监听的全部状态 |
| `-l`, `--listening` | 只看处于 LISTEN（或 UDP 未绑定远端）的套接字 |
| `-n`, `--numeric` | 不解析服务名与主机名，大表下明显更快 |
| `-p`, `--programs` | 显示占用端口的进程名与 PID，跨用户需 root |
| `-s`, `--summary` | 各协议汇总计数，一眼看总量 |
| `-K`, `--kill` | 强制销毁匹配到的 TCP 套接字，需内核支持 `INET_DIAG_DESTROY` |
| `-H`, `-O` | 去掉表头 / 为每个对象打印标签，便于管道处理 |

## 示例

定位是谁占用了 8080：

```bash
ss -ltnp 'sport = :8080'
```

一次列出全部 TCP/UDP 套接字及所属进程，排障起手式：

```bash
ss -tunap
```

统计到数据库端口的已建立连接数，`-H` 让行数即连接数：

```bash
ss -Htn state established '( sport = :5432 )' | wc -l
```

`TIME_WAIT` 堆积是本地端口耗尽的前兆，先用汇总对照：

```bash
ss -s
ss -Hint state time-wait | wc -l
```

Unix domain socket 也在同一条命令里，容器内还要配合对应 netns：

```bash
ss -lx
```

对确认无用的残留连接强制回收：

```bash
ss -K state established dport = :8080
```

## 注意事项

- `-p` 只能看到有权限的进程，跨用户或跨容器需 root；宿主机上看不到其它 netns 的套接字，先 `ip netns exec <name> ss -ltnp`。
- `state` 过滤只对 TCP 有意义，UDP 无连接状态；`netstat -tunap` 与 `ss -tunap` 语义等价，但精简镜像常只装了 iproute2。
- 过滤表达式里的括号、引号与 `:端口` 写法必须完整，写错时不报错而是静默匹配不到任何行。
- `-K` 只作用于 TCP 且不等价于优雅关闭，误用会瞬断业务；先去掉 `-K` 复查匹配范围再执行。

## 相关命令

- `ip` — 确认地址与路由属于哪个接口、哪个网络命名空间
- `lsof -i` — 需要文件描述符级信息时补充
- `ip tcp_metrics` — 查看内核缓存的历史 RTT 与拥塞参数
