# ping — 连通性与往返时延

> 通过 ICMP Echo Request 判断目标是否可达，并给出 RTT 分布、丢包率与抖动。

## 语法

```bash
ping [ OPTIONS ] destination [ data-sizes ]
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-c`, `--count=N` | 发送 N 个请求后退出，脚本里必须显式指定 |
| `-i`, `--interval=N` | 发包间隔秒数，默认 1；小于约 0.2 需 root |
| `-W`, `--timeout=N` | 单个响应等待超时秒数 |
| `-s`, `--size=N` | ICMP 负载字节数，用于大包与 PMTU 测试 |
| `-4`, `-6` | 强制使用 IPv4 或 IPv6 |
| `-D`, `--timestamp` | 每行前加 Unix 时间戳，便于事后对齐日志 |
| `-n`, `--numeric` | 只显示地址，不做反向解析 |
| `-I`, `--interface` | 指定出口网卡或源地址，多网卡机器上关键 |

## 示例

网关基本连通性检查：4 个包、单包 2 秒超时、不解析域名：

```bash
ping -4 -c 4 -W 2 -n 10.0.0.1
```

1472 字节负载加上 28 字节 IP/ICMP 头正好 1500，配 `-M do` 禁止分片来验证链路 MTU：

```bash
ping -c 4 -s 1472 -M do 10.0.0.1
ping -c 4 -s 2000 -M do 10.0.0.1
```

持续观测内网时延并留时间戳，抓周期性抖动：

```bash
ping -i 0.5 -D 10.0.0.20 > /tmp/ping.log
```

双栈域名分别指定协议，判断是 v4 还是 v6 出问题：

```bash
ping -4 -c 3 example.com
ping -6 -c 3 example.com
```

多网卡或期望走特定出口时绑定源地址：

```bash
ping -c 3 -I 10.0.0.12 example.com
```

## 注意事项

- `-f`（flood）与超小 `-i` 间隔需要 root，且对目标相当于打击，只允许在授权的内网段短暂使用。
- 主机或中间设备常对 ICMP 限速，`100% packet loss` 不等于服务宕机，应用 `ss`、`nc -z`、`curl` 复核端口。
- 容器内 ping 依赖 `CAP_NET_RAW`，缺权限时报 `ping: icmp open socket: Operation not permitted`。
- 目标是广播或组播地址时需显式 `-b` 或 `-I <组地址>`，否则会直接拒绝解析。

## 相关命令

- `traceroute` — 逐跳定位丢包发生在哪一段
- `mtr` — 持续采样，把路径与时延统计合成一张报表
- `ip route get` — 先确认 ping 实际选用的出口网卡与源地址
