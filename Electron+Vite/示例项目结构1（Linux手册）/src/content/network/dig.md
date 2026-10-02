# dig — DNS 查询与解析链路定位

> bind-utils 提供的 DNS 诊断工具，直接向指定服务器发问并原样展示各应答分区。

## 语法

```bash
dig [ @server ] [ OPTIONS ] name [ type ] [ class ]
dig [ OPTIONS ] -x address
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `@server` | 指定查询对象，如 `@10.0.0.53`；缺省取 `/etc/resolv.conf` |
| `type` | 位置参数写在名字之后：`A`、`AAAA`、`MX`、`TXT`、`NS`、`SOA`、`CNAME`、`SRV`、`CAA`（旧版本另支持 `-t`） |
| `-x` | 反向查询地址的 `PTR` 记录 |
| `+short` | 只输出答案值，适合脚本 |
| `+noall +answer` | 关闭其余分区，仅保留 ANSWER_SECTION |
| `+trace` | 从根服务器开始迭代查询，展示委派链 |
| `+tcp` | 改用 TCP 53 重查，验证截断与防火墙策略 |
| `+time`, `+tries` | dig 自身的 UDP 超时与重试次数 |
| `-p`, `-4`, `-6` | 非标准 DNS 端口 / 强制协议族 |

## 示例

正向与反向对照，`+short` 每次只拿一类记录：

```bash
dig +short example.com A
dig +short example.com AAAA
dig -x 10.0.0.21 +short
```

去掉 preamble 与统计段，只看真实应答：

```bash
dig example.com +noall +answer
dig example.com MX +noall +answer
```

本机 resolver 与上游转发器各查一次，用差异定位缓存污染或视图错误：

```bash
dig example.com +noall +answer
dig @10.0.0.53 example.com +noall +answer
```

委派没生效时从根开始追，观察是哪一级给出 NS：

```bash
dig example.com NS +trace
```

TXT 或大应答被截断时，先用 TCP、再调 EDNS0 缓冲区确认原因：

```bash
dig example.com TXT +tcp
dig example.com TXT +bufsize=4096 +noall +answer
```

## 注意事项

- `dig` 不走 `/etc/hosts`、NSS 与应用缓存，因此它正常不代表程序能解析；对比 `getent hosts example.com` 才能定位到本机解析层。
- `+short` 在 NXDOMAIN 或空应答时往往不输出任何内容，判断成败要看完整输出的 `status:` 行与 `ANSWER: 0 records`。
- `+trace` 需要直达根与顶级域权威服务器，被防火墙拦截会停在中间；它绕过本地缓存，无法复现客户端看到的旧记录。
- `+time`/`+tries` 只影响 dig 自己的重试；容器里缺 `dig` 时要装 bind-utils/bind-tools，不要临时改用其它名字。

## 相关命令

- `getent hosts` — 验证含 hosts 文件与 NSS 的实际解析结果
- `host`, `nslookup` — 更简单的查询入口，适合快速比对
- `delv` — 需要同时校验 DNSSEC 时使用
- `ss -lun 'sport = :53'` — 确认本机是否真有解析器在监听
