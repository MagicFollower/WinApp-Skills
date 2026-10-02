# fdisk / parted — 查看与划分分区

> `fdisk` 与 `parted` 用于查看分区表、创建分区；写盘操作不可逆，动手前必须逐字确认目标设备。

## 语法

```bash
fdisk -l [设备]          # 只读查看分区表
fdisk [设备]             # 交互界面：p 打印 / n 新建 / d 删除 / w 写入 / q 放弃
parted [-sm] 设备 命令    # 命令行方式，脚本友好
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `fdisk -l` | 列出全部或指定设备的分区表，不做修改 |
| `fdisk -x` | 在 `-l` 之上多输出分区表细节 |
| `fdisk -f` | 配合 `-l` 列出未被分区覆盖的空闲区间 |
| `fdisk -r` | 以只读方式打开设备，防止误写 |
| `parted -l` | 列出所有设备的分区信息 |
| `parted -s` | 静默模式，不交互提示，用于脚本 |
| `parted -m` | 冒号分隔的机器可读输出 |

## 示例

只读查看，安全：

```bash
fdisk -l /dev/vdb
```

```text
Disk /dev/vdb: 500 GiB, 536870912000 bytes, 1048576000 sectors
Disklabel type: gpt
Device     Start       End        Sectors  Size Type
/dev/vdb1   2048 1048573951   1048571948   500G Linux filesystem
```

交互分区：`n` 新建、`p` 打印核对，确认无误才 `w` 写盘；`q` 退出则一条改动都不落盘。

```bash
fdisk /dev/vdb
```

```text
Command (m for help): n
First sector (2048-1048575999, default 2048):
Last sector, +sectors or +size{K,M,G,T,P} (2048-1048575999): +100G
Command (m for help): p
Command (m for help): w
```

脚本里一次做完，从 1 MiB 起对齐；大于 2 TiB 的盘只能用 GPT：

```bash
parted -s /dev/vdb mklabel gpt
parted -s /dev/vdb mkpart data xfs 1MiB 100%
parted -s /dev/vdb print
partprobe /dev/vdb
```

## 注意事项

- **破坏性**：`fdisk` 只有在输入 `w` 之后才写盘；`o`（重建空 MBR 表）、`g`（重建空 GPT 表）、`d`（删分区条目）、`parted ... rm N` 都直接销毁原表。执行前 `dd if=/dev/vdb of=/tmp/vdb.pt bs=512 count=34` 备份分区表，并逐字核对设备名不是系统盘。
- 写盘前用 `p`/`print` 复核，再落 `w`；改分区**起始**扇区等于让原文件系统失效，里面的数据全部报废。
- 只改结束扇区的扩容必须再跑 `resize2fs` 或 `xfs_growfs`，只扩分区不扩文件系统会一直报空间不足。
- MBR 最多 4 个主分区、单分区上限 2 TiB；`parted -s` 遇到需要确认的情况会直接失败而不是猜，目标盘上有分区挂载时内核重读分区表会失败，先卸载再 `partprobe`。

## 相关命令

- `lsblk -f` — 分区后确认设备节点与文件系统类型
- `wipefs -a /dev/vdb1` — 清除旧文件系统签名后再 `mkfs`
- `partprobe` — 通知内核重读分区表
- `mkfs.xfs` / `mkfs.ext4` — 在新分区上建立文件系统
