# traceroute — 逐跳路径与丢包定位

> 通过递增 TTL 观察每一跳的回包，输出链路与各跳时延，用来判断故障落在本机、出口还是骨干段。

## 语法

```bash
traceroute [ OPTIONS ] host [ packetlen ]
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-m`, `--max-hops=N` | 最大跳数，默认 30 |
| `-p`, `--port=N` | 起始目的端口：UDP 探测默认从 33434 开始，TCP 探测默认 80 |
| `-I`, `--icmp` | 用 ICMP Echo Request 代替默认的 UDP 探测 |
| `-T`, `--tcp` | 用 TCP SYN 探测，绕过只放行 TCP 的策略 |
| `-P`, `--proto` | 指定协议：`UDP`、`TCP`、`ICMP` 等 |
| `-n`, `--numeric` | 不做反向解析，出结果更快 |
| `-w`, `--wait=SECS` | 等待单跳应答的秒数 |
| `-q`, `--queries=N` | 每跳发送的探测包数，默认 3 |
| `-s`, `--source=IP` | 指定源地址，多网卡机器上很关键 |
| `-z`, `--sleep-msec=N` | 探测包之间的间隔毫秒数，规避限速 |

## 示例

常规 UDP 逐跳，先看清默认路径与出口：

```bash
traceroute -n -m 20 10.0.0.200
```

UDP 被过滤、整片 `* * *` 时改用 ICMP 与 TCP 对照：

```bash
traceroute -I -n example.com
traceroute -T -p 443 -n example.com
```

每跳 5 个包、等待 2 秒、发包降速，抓周期性丢包：

```bash
traceroute -q 5 -w 2 -z 100 -n 10.0.0.1
```

双网卡机器指定源地址，确认是否走了预期出口：

```bash
traceroute -s 10.0.0.12 -n example.com
```

只关心第一跳就不通（本机到网关）还是整条链路劣化：

```bash
traceroute -m 5 -n 10.0.0.1
```

## 注意事项

- Linux 上 `-I` 与 `-T` 需要原始套接字，即 root 或 `CAP_NET_RAW`；部分发行版给二进制预置了该 capability，无权限时可退回免 priv 的 `tracepath`。
- 中间跳出现 `* * *` 多为路由器对 ICMP TTL exceeded 限速或关闭了解析，不等于该设备宕机；结合末跳与 `ping` 结果一起判断。
- ECMP 负载均衡会让连续探测走不同物理路径，跳址“抖动”属正常；降速（`-z`）能减少被控制平面限速的影响。
- `mtr` 把 traceroute 与 ping 合成持续采样，出报表更适合报障：`mtr -rwc 20 example.com`；`tracepath` 还能顺带测 PMTU。

## 相关命令

- `mtr` — 持续逐跳统计丢包率与时延分布
- `tracepath` — 免特权探测路径与 PMTU
- `ip route get` — 先确认本机为目标选了哪条路由和出口网卡
