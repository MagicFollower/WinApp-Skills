# lsblk — 块设备层级树

> 以树形列出磁盘、分区、LVM、ISO 等块设备及其文件系统与挂载点。

## 语法

```bash
lsblk [选项]... [设备]...
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-f` | 显示 FSTYPE、LABEL、UUID、MOUNTPOINT 等文件系统列 |
| `-p` | 设备名打印成完整路径 `/dev/sda1` |
| `-o NAME,SIZE,TYPE,MOUNTPOINT` | 自定义输出列 |
| `-r` | 原始（raw）表格输出，不带树形前缀，便于管道处理 |
| `--json` | JSON 输出，脚本解析首选 |
| `-n` | 不打印表头 |
| `-a` | 显示空设备与光驱等通常被隐藏的设备 |
| `-d` | 只列出顶层磁盘，不含分区 |
| `-e 7` | 按主设备号排除，例如 `7` 是 loop |

## 示例

拿到一台陌生机器，第一步看清盘与分区全貌：

```bash
lsblk -f
```

```text
NAME    FSTYPE LABEL UUID                                 MOUNTPOINT
sda
├─sda1  vfat   EFI   1234-ABCD                            /boot/efi
├─sda2  ext4   root  6b1c...                              /
└─sda3  swap         9f2a...                              [SWAP]
sdb
└─sdb1  xfs          3d7e...                              /data
sr0     iso9660  CentOS-7                                /mnt
```

新加了盘，确认内核是否已识别、设备名是什么：

```bash
lsblk -o NAME,SIZE,TYPE,FSTYPE,MOUNTPOINT -p
```

给脚本消费，输出稳定的 raw 或 JSON：

```bash
lsblk -rno NAME,SIZE,TYPE,MOUNTPOINT
lsblk -Jf
```

只关心裸盘容量（例如准备 `mkfs` 前）：

```bash
lsblk -d -o NAME,SIZE,MODEL,SERIAL,TRAN
```

## 注意事项

- `lsblk` 只做只读枚举，不会修改分区表；但 `-o` 里写错列名时只会报错退出，不输出任何行，脚本要先判返回码。
- 刚改完分区表看不到新分区，是内核未重读，用 `partprobe` 或 `blockdev --rereadpt`，不是 `lsblk` 的问题。
- UUID 与 LABEL 只有已建立文件系统的设备才有；分区本身的 `PARTUUID`/`PARTLABEL` 需要显式加到 `-o` 里。
- 较新版本 util-linux 把列名改为 `MOUNTPOINTS`（复数），同一分区被 bind 到多处时会全部列出；旧环境仍写 `MOUNTPOINT`，脚本里两种都做一次兼容判断更稳。

## 相关命令

- `blkid` — 只关心 UUID / LABEL / TYPE 时使用
- `df -hT` — 查看已挂载文件系统的空间用量
- `parted -l` — 查看分区表类型与分区边界
- `lscpu` / `lsmem` — 同一套 util-linux 硬件枚举工具
