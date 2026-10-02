# lsof — 列出被进程打开的文件与端口

> 一切皆文件：`lsof` 把普通文件、套接字、管道、块设备以及「已删除但仍被打开」的文件都关联到持有它的进程。

## 语法

```bash
lsof [选项] [文件...]
lsof -i [4|6][@主机][:端口]
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-i` | 只看网络文件：`-i :8080`、`-iTCP -sTCP:LISTEN`、`-i udp` |
| `-p 1234` | 按 PID 过滤，逗号分隔多个 |
| `-c nginx` | 按进程名前缀过滤 |
| `+D /var/log` | 递归列出目录下所有被打开的文件 |
| `+L1` | 链接计数小于 1，即已删除但仍被打开的文件 |
| `-a` | 把多个条件的默认「或」改为「与」 |
| `-P` / `-n` | 显示端口号而非服务名 / 不解析主机名 |
| `-t` | 只输出 PID，便于 `kill $(lsof -t <文件>)` |

## 示例

端口被占、报 `bind: address already in use` 时直接找凶手：

```bash
lsof -i :8080 -P -n
```

```text
COMMAND   PID USER   FD   TYPE DEVICE SIZE/OFF NODE NAME
node    21455 root   21u  IPv4  91234      0t0  TCP *:8080 (LISTEN)
```

谁在用这个目录（`+D` 会递归整棵树，路径已知时直接写文件名更快）：

```bash
lsof +D /var/log
```

看某进程打开了哪些文件：`FD` 里 `cwd` 是当前目录、`txt` 是可执行映像，末尾 `u/r/w` 表示读写方式。

```bash
lsof -p 21455
lsof -a -p 21455 -i   # 组合条件加 -a，否则 -p 与 -i 之间是「或」
```

删了大日志但 `df` 仍然满，用 `+L1` 找持有者；`NLINK` 为 0、路径带 `(deleted)` 就是它，`SIZE/OFF` 即被白占的字节数。

```bash
lsof +L1
: > /proc/812/fd/5    # 确认可以打断后，截断该 fd 立即回收空间
```

```text
COMMAND   PID USER   FD   TYPE DEVICE  SIZE/OFF NLINK  NODE NAME
rsyslogd  812 root    5w   REG  253,1 214748364     0 65540 /var/log/messages (deleted)
```

## 注意事项

- 非 root 只能看到自己进程的打开文件，「查无此人」往往是权限不足，排障请 `sudo lsof`。
- 更稳妥的回收方式是重启持有者进程；直接截断 `/proc/<PID>/fd/<N>` 要求该 fd 以可写方式打开。
- `SIZE/OFF` 是文件偏移量而不是已落盘大小，稀疏文件与正在写入的日志会显示得比实际占用大。
- 刚升级内核后 `lsof` 可能报 `/proc` 相关警告，加 `-w` 抑制，并用 `ss -p`、`fuser` 交叉验证结论。

## 相关命令

- `fuser -mv <挂载点>` — 快速列出使用某文件/目录/设备的进程
- `ss -lntp` — 只看监听端口与所属进程，比 `lsof -i` 开销小
- `df -h` / `df -i` — 对照块与 inode 用量，判断空间被谁占
