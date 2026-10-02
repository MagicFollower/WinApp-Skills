# mv — 移动与重命名

> 同文件系统内移动或改名是原子操作；跨文件系统时退化为复制再删除。

## 语法
```bash
mv [OPTION]... [-T] SOURCE DEST
mv [OPTION]... SOURCE... DIRECTORY
mv [OPTION]... -t DIRECTORY SOURCE...
```

## 常用选项
| 选项 | 说明 |
| --- | --- |
| `-i` | 目标已存在时询问 |
| `-n` | 目标已存在则跳过该文件 |
| `-u` | 仅当源比目标新时才移动 |
| `-f` | 不询问，直接覆盖（覆盖交互模式的默认 alias） |
| `-t DIR` | 多个源统一移动到目录 `DIR` |
| `-T` | 把目标当普通文件处理，即使它是已存在目录 |
| `-v` | 打印每次移动的结果 |
| `-b`, `--backup[=METHOD]` | 覆盖前先备份，如 `--backup=numbered` |

## 示例
重命名单个文件：

```bash
mv report.pdf 2026-Q1-report.pdf
```

把多个文件移动进同一目录：

```bash
mkdir -p /srv/www/static
mv -t /srv/www/static/ app.js style.css logo.svg
```

含空格的路径必须加引号：

```bash
mv "2026 年度总结.docx" /srv/docs/
```

用临时文件生成新配置后原子替换，避免读到半成品：

```bash
sed 's/^#Port .*/Port 8443/' /etc/ssh/sshd_config > /tmp/sshd_config.new
sudo mv -f /tmp/sshd_config.new /etc/ssh/sshd_config
```

批量整理编号文件：

```bash
mkdir -p photos
for f in IMG_*.JPG; do mv -n "$f" "photos/${f#IMG_}"; done
```

## 注意事项
- 目标名恰好是已存在目录时，`mv src dst` 会把 `src` 放进 `dst/` 而不是改名；想强制重命名用 `-T`。
- 跨文件系统移动（`EXDEV`）是复制加删除，非原子、耗时且可能中途失败，大目录建议先 `rsync -a` 再删除。
- 改名需要源目录和目标目录的写权限；源文件本身只读不影响移动，因为它不修改文件内容。
- 同设备移动后属主、权限、时间戳都不变，inode 相同；跨设备复制则受 `cp` 类规则约束。
- 交互式 shell 常有 `alias mv='mv -i'`，脚本里默认是静默覆盖。

## 相关命令
- `cp` — 保留原文件，复制并可保留属性
- `rsync` — 跨设备或跨主机移动，支持断点与增量
- `rename` — 按表达式批量重命名（需另行安装）
- `ln` — 创建硬链接或符号链接代替复制
