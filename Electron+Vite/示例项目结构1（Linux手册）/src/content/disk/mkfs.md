# mkfs — 在设备上创建文件系统

> `mkfs.ext4` / `mkfs.xfs` 会在目标设备上建立全新文件系统，**原有数据立即销毁且不可恢复**。

## 语法

```bash
mkfs [-t 类型] [文件系统选项] 设备      # 前端，实际调用 /sbin/mkfs.<类型>
mkfs.ext4 [选项] 设备
mkfs.xfs [选项] 设备
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-t ext4` | 通过 `mkfs` 前端指定类型 |
| `-L label` | 设置卷标，ext4 与 xfs 都支持 |
| `-m 1` | ext4 给 root 预留的空间比例，默认 5%，纯数据盘常降到 1% |
| `-P` | mkfs.ext4 试运行，打印超级块与组描述符但不写盘 |
| `-F` | mkfs.ext4 跳过确认、允许在非常规设备上继续 |
| `-f` | mkfs.xfs 强制覆盖设备上已有的文件系统 |
| `-b 4096` | ext4 块大小 |
| `-i size=512` | xfs inode 大小 |
| `-l size=256m` | xfs 内部日志大小 |

## 示例

动手前先确认这台机器上这块盘到底是谁，这一步不能省：

```bash
lsblk -f /dev/vdc
blkid /dev/vdc
findmnt /dev/vdc1
```

用 dry-run 看清 mkfs.ext4 会写出什么参数，不落盘：

```bash
mkfs.ext4 -P -L data -m 1 /dev/vdc1
```

正式创建 ext4：卷标 `data`、预留 1%、关掉后台初始化以免首次写入被拖慢：

```bash
mkfs.ext4 -L data -m 1 -E lazy_itable_init=0,lazy_journal_init=0 /dev/vdc1
```

创建 XFS（`-f` 表示覆盖已有文件系统，只在该设备已确认后使用）：

```bash
mkfs.xfs -f -L data -i size=512 /dev/vdc1
```

建完立刻核对并挂载：

```bash
blkid /dev/vdc1
mkdir -p /data && mount /dev/vdc1 /data && df -hT /data
```

## 注意事项

- **不可逆**：`mkfs` 直接重写超级块与日志，旧内容立刻不可读。执行前三步确认 —— `lsblk -f` 看层级、`blkid` 看现有 UUID 与卷标、`findmnt` 确认未挂载；带数据的盘先 `umount`，绝不能对根分区或正在用的 swap 执行。
- `/dev/sdb` 这类设备名会随插拔和重启漂移，核对容量、序列号与分区号后再写命令，最好用 `/dev/disk/by-id/` 下的全路径。
- 没加 `-F`/`-f` 时 mkfs 检测到旧签名会停下来要求确认，这是保护而非故障；先 `wipefs -a` 清签名再建，别一路 `-f` 蒙着上。
- ext4 建完仍可用 `tune2fs` 调预留比例等参数，还能用 `resize2fs` 扩缩；XFS 只能扩（`xfs_growfs`）不能缩，容量规划要一次定死。

## 相关命令

- `blkid` — 查看设备上的文件系统类型与 UUID
- `wipefs` — 列出/清除文件系统与分区表签名
- `tune2fs -l` — 查看 ext4 超级块参数
- `xfs_info /data` — 查看已挂载 XFS 的几何参数
