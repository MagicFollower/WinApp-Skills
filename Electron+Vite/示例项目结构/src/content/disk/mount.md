# mount / umount — 挂载与卸载

> 挂载已有文件系统的设备或按 `/etc/fstab` 批量挂载；卸载报 busy 时用 `fuser` 找出占用者。

## 语法

```bash
mount [-t 类型] [-o 选项] <设备> <挂载点>
mount -a | mount -L <label> <挂载点> | mount -U <uuid> <挂载点>
umount [-fl] <挂载点或设备>...
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `mount -t ext4` | 指定文件系统类型，省略时由内核探测 |
| `mount -o ro,noatime` | 逗号分隔的挂载选项 |
| `mount -o remount` | 已挂载状态下调整选项，不中断访问 |
| `mount -B` | 绑定挂载，把已有目录映射到另一路径 |
| `mount -a` | 按 `/etc/fstab` 挂载所有未挂载的条目 |
| `mount -L` / `-U` | 按 LABEL / UUID 定位设备 |
| `umount -l` | 延迟卸载：先摘目录树，引用释放后再真正卸载 |
| `umount -f` | 强制卸载，用于 NFS 服务端不再响应 |

## 示例

标准挂载并确认落点：

```bash
mkdir -p /data
mount /dev/sdb1 /data && findmnt /data
```

取 UUID 写入 `/etc/fstab`，每行格式是 `<设备> <挂载点> <类型> <选项> <dump> <fsck>`。
用 UUID 而不是设备名，因为 `/dev/sdb` 会随插拔漂移；写完当场用 `mount -a` 验证，别等重启报错：

```bash
blkid /dev/sdb1                 # 取 UUID 填到下面这行
mount -a && findmnt /data       # 写入 fstab 后当场验证
```

```text
# /etc/fstab
UUID=3d7e1c52-1a0b-4f9d-9c2f-7b0a5d1e8834  /data  xfs  defaults,noatime  0 0
```

卸载报 `target is busy` 时先找占用者，确认可以打断再结束进程：

```bash
fuser -mv /data
lsof +D /data
fuser -k /data && umount /data
```

bind 挂载，把宿主目录暴露给 chroot 或容器：

```bash
mount -B /data/shared /srv/app/data
findmnt /srv/app/data
```

## 注意事项

- `mount`/`umount` 通常需要 root；非 root 只有 `fstab` 中带 `user`/`users` 选项的条目才允许。
- `umount -l` 会立刻摘掉挂载点，但写入仍可能落在正在卸载的文件系统上；数据敏感场景先停业务再常规卸载，`-f` 只在服务端 hang 时使用。
- `fstab` 的 `dump`/`fsck` 两列：根分区写 `0 1`，其他本地盘 `0 2`，swap 与网络挂载写 `0 0`；填错会让启动卡在 emergency mode。
- 卸载前确认没有进程把工作目录留在挂载点里（`fuser -mv` 输出中的 `cwd` 标记），否则看似卸载成功、空间却不释放。

## 相关命令

- `findmnt` — 树形查看当前挂载关系与生效选项
- `lsblk -f` — 查看设备、文件系统类型与挂载点
- `swapon -a` / `swapoff -a` — 按 fstab 启用或停用 swap
