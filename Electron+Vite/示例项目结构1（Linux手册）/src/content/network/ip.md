# ip — 地址、链路、路由的统一入口

> iproute2 的核心命令，用 `ip link` / `ip address` / `ip route` 取代 ifconfig、route 等 net-tools 旧工具。

## 语法

```bash
ip [ OPTIONS ] OBJECT { COMMAND | ARGUMENTS }
OBJECT := link | addr | route | rule | neigh | monitor
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-s`, `--stats` | 追加收发包、字节、错误与丢包计数 |
| `-br`, `-brief` | 每条记录压成对齐的单行，适合脚本与快速核对 |
| `-4`, `-6` | 限定只处理 IPv4 或 IPv6 对象 |
| `-d`, `--details` | 显示协议、来源表等附加字段 |
| `-j`, `-json` | JSON 输出，便于交给 `jq` 解析 |

## 示例

先总览接口状态与地址，`state` 列给出的 UP/DOWN 比 ifconfig 更贴近内核：

```bash
ip -br link
ip -br addr show
```

启用网卡并确认计数开始增长，排查“插了线但没流量”：

```bash
ip link set dev eth1 up
ip -s link show dev eth1
```

给 eth0 临时追加一个地址，验证完再删；这类改动重启即失效：

```bash
ip addr add 10.0.0.128/24 dev eth0
ip addr del 10.0.0.128/24 dev eth0
```

查默认路由并替换网关，再确认到目标的实际出口：

```bash
ip route show
ip route replace default via 10.0.0.1 dev eth0
ip route get 10.0.0.9
```

实时监听链路和地址变动，定位 DHCP 续约或容器启停引发的接口抖动：

```bash
ip monitor link addr
```

## 注意事项

- 读操作用户态可跑，`link set`、`addr add`、`route replace` 修改内核网络栈需要 root 或 `CAP_NET_ADMIN`。
- 运行时配置不持久，落盘要交给 NetworkManager（`nmcli`）、systemd-networkd 或 `/etc/network/interfaces`。
- net-tools 的 `ifconfig`/`route` 在新发行版常需单独安装；`ip` 输出的 `state`、`metric`、`scope` 字段更完整。
- 远程操作时误 `down` 掉 SSH 所在网卡会立刻断连，先用 `ip netns` 或 `screen` 兜底，并把 up/down 写成一条命令。

## 相关命令

- `ss` — 查看这些地址与端口上实际承载的套接字
- `ip neigh` — 查看与清理 ARP / IPv6 ND 缓存
- `nmcli` — 让地址、路由与 DNS 改动持久生效
