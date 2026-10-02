# curl — 请求发送与响应检查

> 支持 HTTP/HTTPS 等协议的命令行客户端，排障时用来观察真实请求、响应头与耗时分解。

## 语法

```bash
curl [ OPTIONS ] [ URL ... ]
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-I`, `--head` | 只发 HEAD 请求取响应头 |
| `-o`, `--output <file>` | 把响应体写入指定文件，`-o -` 输出到 stdout |
| `-O`, `--remote-name` | 按 URL 中的文件名保存到当前目录 |
| `-L`, `--location` | 跟随 3xx 重定向，可配 `--max-redirs` |
| `-H`, `--header <h>` | 添加或覆盖请求头，可重复使用 |
| `-d`, `--data <s>` | 以 POST 发送数据，`-d @file` 从文件读取 |
| `-u`, `--user user:pass` | Basic 认证；凭据会出现在进程列表 |
| `--retry N`, `--retry-delay N` | 瞬时错误自动重试及间隔秒数 |
| `-sS` | 静默进度条但保留错误信息 |
| `-w`, `--write-out <fmt>` | 输出 `%{http_code}`、`%{time_total}` 等变量 |
| `-f`, `--fail` | 4xx/5xx 时返回非 0 退出码 |

## 示例

只看状态码与重定向目标，判断 CDN 或反代的跳转配置：

```bash
curl -sSI -o /dev/null -w '%{http_code} %{redirect_url}\n' http://example.com/
```

把 DNS、TCP、TLS、总计耗时逐项打出来，定位慢在哪一层：

```bash
curl -sS -o /dev/null \
  -w 'dns=%{time_namelookup} tcp=%{time_connect} tls=%{time_appconnect} ttfb=%{time_starttransfer} total=%{time_total}\n' \
  https://example.com/
```

发送 JSON POST，`-H` 声明类型、`--data-binary` 保留原始字节：

```bash
curl -sS -X POST https://example.com/api/v1/devices \
  -H 'Content-Type: application/json' \
  -H 'Authorization: Bearer TOKEN' \
  --data-binary '{"ip":"10.0.0.12","reboot":false}'
```

下载发行包并保留远端文件名，失败重试 3 次、每次间隔 2 秒：

```bash
curl -u ops:secret -L -O --retry 3 --retry-delay 2 https://example.com/releases/agent.tar.gz
```

内网自签证书站点：临时验证用 `-k`，长期方案是指定 CA：

```bash
curl -sS --cacert /etc/ssl/internal-ca.pem https://10.0.0.8/health
```

## 注意事项

- `-O` 与 `-o` 语义不同：前者用远端文件名，后者必须给路径；管道给 `tar` 时用 `-o -` 并配 `-L`。
- `-d` 已隐含 POST，多余的 `-X POST` 在跟随重定向后会把本应的 GET 也变成 POST。
- 默认 4xx/5xx 仍返回退出码 0，脚本里要加 `-f` 或检查 `-w '%{http_code}'`。
- 同时设 `--connect-timeout` 与 `--max-time`，否则 TCP 半开或对端不发包会长时间挂住进程。

## 相关命令

- `wget` — 只需下载与递归抓取时更直观
- `dig` — 报 `Could not resolve host` 时先查解析链路
- `ss` — 确认对端端口是否有人监听，再判断是不是应用层问题
