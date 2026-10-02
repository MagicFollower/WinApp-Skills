# scp — 经由 SSH 的跨主机复制

> 走 SSH 通道复制文件；OpenSSH 9.0 起默认使用 SFTP 协议传输，行为与旧 rcp 风格已有差异。

## 语法

```bash
scp [ OPTIONS ] [ [user@]host1:]file1 ... [ [user@]host2:]file2
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-r`, `--recursive` | 递归复制目录 |
| `-P`, `--port` | 远端 SSH 端口（大写 P；小写 `-p` 是保留属性） |
| `-p`, `--preserve` | 保留修改时间、访问权限与属主信息 |
| `-l`, `--limit-rate` | 限速，单位 Kbit/s |
| `-C` | 传输压缩，对已压缩文件无收益 |
| `-i`, `-J` | 指定私钥 / 经跳板机中转 |
| `-3` | 两台远端之间互传，数据经本机中转 |
| `-O`, `--legacy-protocol` | 强制旧的 SCP/RCP 协议，仅在服务端无 sftp 子系统时使用 |
| `-o`, `-S`, `-T` | 传 ssh 配置项 / 指定替代 ssh 程序 / 关闭进度统计 |

## 示例

拉取整个日志目录并保留时间戳，便于之后按 mtime 排序：

```bash
scp -rpP 2200 ops@10.0.0.21:/var/log/app/ ./app-logs/
```

推送配置到内网机器，`BatchMode=yes` 禁止任何交互提示，适合放进发布脚本：

```bash
scp -o BatchMode=yes -P 2200 agent.conf ops@10.0.0.21:/etc/app/
```

经跳板机传输，`-J` 同样适用于 scp：

```bash
scp -J ops@10.0.0.2 -P 22 backup.tar.gz ops@10.0.0.30:/srv/backup/
```

远端通配符交给远端 shell 展开，本地加引号避免被本机提前吃掉：

```bash
scp ops@10.0.0.21:'/var/log/app/*.log' ./
```

限制带宽，避免白天挤占业务链路：

```bash
scp -l 8000 big.iso ops@10.0.0.30:/srv/tmp/
```

## 注意事项

- 目标存在与否决定语义：`scp file host:dir/` 要求 `dir` 已存在，否则会被当成新文件名；同名文件直接覆盖且无提示。
- 大量小文件或需要断点续传请改用 `rsync -az --partial --progress`，scp 中断只能整文件重来；跨网段还应对比 `rsync -e "ssh -p 2200"`。
- 服务端未启用 sftp 子系统时新版 scp 会报 `subsystem request failed`，此时用 `-O` 退回旧协议，或改 `sftp -P 2200 host:/path` 做交互式 `mget/mput`。
- 传输失败常源于目标目录不可写而非协议本身，先用 `ssh host 'ls -ld /etc/app'` 确认权限与磁盘余量。

## 相关命令

- `rsync` — 增量同步、续传与删除远端多余文件
- `sftp` — 目录浏览与批量交互传输，是当前推荐的替代方向
- `ssh` — 远端直接执行 `tar` 管道，绕开逐文件开销
