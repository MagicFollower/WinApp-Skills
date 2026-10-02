# nc — 端口探测与极简 TCP/UDP 服务

> netcat 在任意 TCP/UDP 端口上读写数据，用来验证连通性、手搓明文协议交互或两台机器间直传文件。

## 语法

```bash
nc [ OPTIONS ] <host> <port>        # 客户端 / 探测
nc -l [ OPTIONS ] [ <port> ]        # 监听端
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-z` | 扫描模式：只试探端口能否连接，不发送数据 |
| `-l` | 进入监听模式 |
| `-p` | OpenBSD/traditional 监听端口写法；GNU 版里表示绑定源端口 |
| `-u` | 使用 UDP，默认是 TCP |
| `-v`, `-n` | 详细输出 / 不做主机名与服务名解析 |
| `-w <sec>` | 连接与读取超时，脚本里几乎必配 |
| `-i <sec>` | 行或包之间的发送间隔，用于观察限速协议交互 |
| `-k` | 处理完一个连接后继续监听（OpenBSD、ncat 支持） |
| `-q`, `-N` | stdin 结束后的延迟关闭 / 立即关闭（traditional 用 `-q`，OpenBSD 用 `-N`） |
| `-e <prog>` | 收到连接后执行程序，仅 traditional/ncat 有，OpenBSD 版出于安全移除 |
| `-U` | 连接 UNIX domain socket（OpenBSD 版） |

## 示例

探测端口是否可达，配 `-w` 防止被静默丢弃的防火墙拖住：

```bash
nc -vz -w 2 10.0.0.21 22
nc -vz -w 1 10.0.0.21 1-1024
nc -vz -u 10.0.0.53 53
```

手工发一个 HTTP 请求，看服务返回的原始报文：

```bash
printf 'GET / HTTP/1.0\r\nHost: example.com\r\n\r\n' | nc -N example.com 80
```

一端监听写入文件，另一端重定向发送；无完整性校验，传完必须比对 `sha256sum`：

```bash
nc -l -p 9000 > app.tar.gz          # GNU 版写作 nc -l 9000
nc -N 10.0.0.5 9000 < app.tar.gz
```

用 fifo 做双向中继，把内网某个端口临时透出：

```bash
rm -f /tmp/relay && mkfifo /tmp/relay
nc -l -p 9001 < /tmp/relay | nc 10.0.0.5 9000 > /tmp/relay
```

冒烟测试一个极简 HTTP 应答，OpenBSD 版可直接 `-k` 循环：

```bash
while true; do printf 'HTTP/1.0 200 OK\r\n\r\nok' | nc -l -p 8080; done
```

## 注意事项

- 发行版里的 `nc` 可能是 netcat-openbsd、netcat-traditional、GNU netcat 或 nmap 的 ncat，选项不完全兼容；用 `update-alternatives --display nc` 与 `nc -h` 确认本机实际支持哪套写法。
- `-z` 端口扫描会触发 IDS 与云安全组封禁，只在获得授权的 `10.0.0.0/24` 这类内网段使用。
- 明文且无认证加密，跨网段传文件请改用 `scp`、`rsync` 或 ncat 的 `--ssl`。
- 以 `nc -l` 起的服务没有任何访问控制，用完立刻 `Ctrl-C` 并 `ss -ltnp` 复查端口是否释放。

## 相关命令

- `ss` — 确认监听是否真的建立、由哪个进程持有
- `curl` — 需要 TLS、认证与状态码判断时不要用 nc 凑
- `traceroute` — 端口不通时先看路径是否可达
