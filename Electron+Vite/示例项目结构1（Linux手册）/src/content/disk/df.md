# df — 文件系统空间与 inode 使用

> 报告各挂载点已用/可用的块数与 inode 数，是「磁盘写满」告警的第一现场。

## 语法

```bash
df [选项]... [文件]...
```

不带参数时列出所有文件系统；给出文件名时，只显示包含该文件的那个文件系统。

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-h`, `--human-readable` | 以 1024 进制的 K/M/G/T 显示 |
| `-H`, `--si` | 以 1000 进制的 k/M/G/T 显示，数值与 `-h` 不同 |
| `-i`, `--inodes` | 显示 inode 用量而不是块用量 |
| `-T`, `--print-type` | 多输出一列文件系统类型 |
| `-t ext4` | 只显示指定类型，可重复指定多个 |
| `-x tmpfs` | 排除指定类型，可重复指定多个 |
| `--total` | 末尾追加汇总行 |
| `-P`, `--portability` | 强制单行输出，便于脚本按列切分 |

## 示例

先看块用量，再看 inode 用量，两者任一个耗尽都会写入失败：

```bash
df -h
df -i
```

```text
Filesystem      Size  Used Avail Use% Mounted on
/dev/vda1        50G   45G  5.2G  90% /
/dev/vdb1       500G  498G     0 100% /data
tmpfs           395M  1.2M  394M   1% /dev/shm
```

只看真实磁盘文件系统并带汇总行，屏蔽 tmpfs/udev 之类的噪音（`--total` 只累加实际列出的行）：

```bash
df -hT -t ext4 -t xfs -x tmpfs -x devtmpfs --total
```

定位某个数据目录落在哪个文件系统上：

```bash
df -h /var/lib/mysql
```

设备名很长导致输出折行、awk 取列错位时，用 `-P` 保证一行一条并直接筛高水位：

```bash
df -hP | awk 'NR>1 && $5+0 >= 80 {print $1, $5, $6}'
```

## 注意事项

- `Use%` 显示 100% 但 `Avail` 仍有几 G：ext2/3/4 默认为 root 预留 5% 块（`-m 5`），普通用户写不进去，用 `tune2fs -m 1 /dev/vda1` 调整预留比例。
- `df -i` 中 `IUse%` 到 100% 时，即使空间充足也无法创建新文件，常见于海量小文件/未清理的 session 与 mail 队列目录。
- 已删除但仍被进程持有的文件不会立即释放空间，`df` 会持续显示占满，需用 `lsof +L1` 找出持有者。
- NFS/CIFS 上 `df` 依赖服务端应答，服务端 hang 时命令一起卡住，可加 `timeout 5 df -h` 或改用 `stat -f`。

## 相关命令

- `du -sh /var/*` — 逐级定位真正占空间的目录
- `lsblk -f` — 查看块设备层级与文件系统类型
- `tune2fs -l /dev/vda1` — 查看 ext4 预留块比例与 inode 总数
- `free -h` — 查看内存与 swap 使用
