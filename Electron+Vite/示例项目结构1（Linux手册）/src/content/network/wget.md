# wget — 稳健下载与断点续传

> 非交互下载工具，支持续传、递归抓取与时间戳比对，适合在无人值守的服务器上拉文件。

## 语法

```bash
wget [ OPTIONS ] [ URL ... ]
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-c`, `--continue` | 从已下载字节处续传，需服务端支持 `Range` |
| `-q`, `--quiet` | 完全静默；排障时改用 `-nv` 只去掉进度条 |
| `-r`, `--recursive` | 递归抓取页面内的链接 |
| `-np`, `--no-parents` | 递归时不越出给定目录层级 |
| `-P`, `--directory-prefix` | 指定保存目录前缀 |
| `-O`, `--output-document` | 指定输出文件名，`-O -` 写到 stdout |
| `--limit-rate=200k` | 限速，避免抢占生产带宽 |
| `--show-progress` | 静默或日志模式下仍显示进度条 |
| `-t`, `-T`, `-w` | 重试次数 / 网络超时秒数 / 重试间隔 |
| `-l`, `-nH`, `-k` | 递归深度 / 不建主机目录 / 改写链接为本地可跳 |

## 示例

大文件中断后续传，非 tty 环境用 `--progress=bar:force` 保留进度输出：

```bash
wget -c --limit-rate=10M --show-progress https://example.com/releases/agent.tar.gz
```

放到指定目录并按本地名字保存，注意 `-O` 只接文件名而不接目录：

```bash
wget -P /var/tmp/ -O /var/tmp/latest.bin https://example.com/data/current
```

镜像内网文档站，限深、不越级并改写站内链接：

```bash
wget -r -np -nH -k -l 3 --limit-rate=5M -P /srv/mirror/ http://10.0.0.20/docs/
```

不落盘直接管道解压：

```bash
wget -q -O - https://example.com/app.tar.gz | tar -xz
```

脚本中只关心成败，把日志追加到文件：

```bash
wget -nv -t 3 -T 30 -c https://example.com/pkg.rpm -O /tmp/pkg.rpm 2>>/var/log/fetch.log
```

内网自签证书：补 CA，而不是关校验：

```bash
wget --ca-certificate=/etc/ssl/internal-ca.pem https://10.0.0.8/firmware.bin -P /srv/
```

## 注意事项

- `-r` 默认遵守 `robots.txt`，被拒时可用 `-e robots=off` 绕过，但务必配 `-np`、`-l` 与限速，否则容易压垮对方站。
- `-c` 遇到服务端不支持 `Range` 或 `Content-Length` 变化会从头重下；与 `-O` 组合时目标文件必须已存在。
- 需要请求头时写 `--header='Authorization: Bearer ...'`，反复试错不如直接换 curl。
- 超时与重试默认为 `-T` 900 秒、`-t` 20 次，健康检查类脚本要显式收紧，避免长时间阻塞。

## 相关命令

- `curl` — 需要精细控制请求头与输出变量时更合适
- `rsync` — 大批量跨主机增量同步
- `wget -i list.txt` — 从文件读取 URL 列表批量下载
