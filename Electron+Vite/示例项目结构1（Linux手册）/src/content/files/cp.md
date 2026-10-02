# cp — 复制文件与目录

> `-a` 归档复制并保留属性；`-i`/`-n`/`-u` 控制覆盖，`-t` 统一指定目标目录。

## 语法
```bash
cp [OPTION]... [-T] SOURCE DEST
cp [OPTION]... SOURCE... DIRECTORY
cp [OPTION]... -t DIRECTORY SOURCE...
```

## 常用选项
| 选项 | 说明 |
| --- | --- |
| `-r`, `-R`, `--recursive` | 递归复制目录内容 |
| `-a`, `--archive` | 等于 `-dR --preserve=all`，尽量原样保留 |
| `-p` | 保留模式、属主、属组、时间戳 |
| `--preserve=LIST` | 指定保留项，如 `mode,ownership,timestamps,context` |
| `-d` | 等于 `--no-dereference --preserve=links`，保留符号链接本身 |
| `-L` | 跟随符号链接，复制其指向的内容 |
| `-i` | 覆盖前询问 |
| `-n` | 目标已存在则跳过，不覆盖 |
| `-u` | 仅当源更新或目标不存在时才复制 |
| `-t DIR` | 把多个源统一复制到目录 `DIR` |
| `-T` | 把目标当普通文件，即使它是已存在的目录 |
| `-v` | 逐个打印复制过程 |
| `--backup[=METHOD]`, `-b` | 覆盖前先备份，如 `--backup=numbered` |
| `--reflink=auto` | 支持的文件系统（btrfs、XFS、OCFS2）上做写时克隆 |

## 示例
整目录备份并保留权限、属主、时间戳与符号链接：

```bash
sudo cp -a /etc/nginx /etc/nginx.bak
```

多个文件统一复制进同一目录，用 `-t` 免去重复写目标：

```bash
cp -t /srv/backup/ report.pdf notes.md data.csv
```

只同步比目标更新的文件，适合增量归档：

```bash
sudo cp -uv /var/log/app/*.log /mnt/nas/logs/
```

保留链接本身与复制链接内容，两种写法对比：

```bash
ln -s /etc/hosts hosts-link
cp -d hosts-link copy-as-link
cp -L hosts-link copy-as-file
```

用新配置覆盖旧文件，先留一份编号备份：

```bash
sudo cp -b --backup=numbered sshd_config.new /etc/ssh/sshd_config
```

## 注意事项
- 目标是已存在目录时会在其中创建同名条目；要复制成新名字必须给出完整目标路径，或用 `-T` 强制按文件处理。
- 默认覆盖是写入已存在的 inode，目标的属主与权限保持不变；想换成新 inode 用 `--remove-destination`。
- 非 root 用 `-a`/`-p` 无法保留属主，会告警并把属主变成执行者；跨用户备份需要 `sudo`。
- `-r` 只保证递归，不保留属性；迁移目录、做备份应当用 `-a`。
- 通配符不含隐藏文件，`cp -r dir/* dest/` 会漏掉 `.file`，直接写 `cp -a dir/. dest/` 更稳。

## 相关命令
- `rsync` — 增量复制并保留属性，适合大目录与远程同步
- `mv` — 同设备移动/重命名，比先复制再删除更快
- `install` — 复制同时设置权限与属主
- `tar` — 打包复制，保留属性与链接结构
