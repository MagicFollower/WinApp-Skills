# zip — 打包并逐个压缩

> Info-ZIP zip 把文件和目录打包成 `.zip`，每个成员单独用 deflate 压缩，可随机读写单个条目，Windows 资源管理器可直接打开。

## 语法

```bash
zip [选项] 归档.zip [文件...]
zip -r 归档.zip 目录/          # 递归打包目录
zip -d 归档.zip 成员名          # 从归档中删除条目
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-r`, `--recurse` | 递归处理目录 |
| `-0` … `-9` | 压缩级别，`-0` 只存储不压缩（等同 `--store`），`-9` 最小最慢，默认 `-6` |
| `-x`, `--exclude` | 排除匹配的模式，必须写在文件参数之后，且模式加引号 |
| `-e`, `--encrypt` | 交互式输入密码（传统 PKWARE 加密） |
| `-P`, `--password` | 在命令行给出密码，同用户可用 `ps` 看到，只适合临时用途 |
| `-u`, `--update` | 更新已存在的条目并追加新文件 |
| `-d`, `--delete` | 从归档中删除指定条目 |
| `-FS`, `--filesync` | 让归档内容与目录完全一致（源目录已删除的文件会从归档中移除） |
| `-j`, `--junk` | 只存文件名，不保留路径 |
| `-y`, `--symlinks` | 保存符号链接本身而不是其目标 |
| `-T`, `--test` | 校验归档完整性 |

## 示例

打包目录并排除版本控制、依赖与日志；`-x` 的模式要在文件参数之后并加引号：

```bash
zip -r9 release.zip app/ -x "app/.git/*" "app/node_modules/*" "*.log"
```

和 tar.gz 的关键差别是 zip 可以就地更新与删除条目：

```bash
zip -u release.zip app/config/app.yaml
zip -d release.zip "app/*.log"
```

`-0` 只归档不压缩，适合图片、视频这类已经是压缩数据的内容：

```bash
zip -0 photos.zip -r img/
```

加密归档与完整性校验：

```bash
zip -e9 secret.zip docs/*.pdf    # 交互式输入密码
zip -T release.zip && echo "archive ok"
```

用 `-f`/`-FS` 维护一个长期使用的归档，避免重复创建：

```bash
cd /srv/app && zip -FS ../snapshot.zip -r . -x "*.log"
```

## 注意事项

- zip 每个条目独立压缩，单个条目损坏不影响其余文件；缺点是总体压缩率通常低于 `tar -czf`（后者把整棵树当一个流压，符号信息与相邻文件可互相供词典）。跨平台分发选 zip，纯 Linux 备份选 tar.gz/xz。
- 归档不存在时新建，存在时**追加/更新**而不是覆盖；想从干净状态重建，先 `rm -f 归档.zip` 或 `zip -s 0 归档.zip --out 新.zip`。
- zip 的 Unix 权限只保存在扩展属性里，Windows 解压一般忽略；属主信息不保存。
- `-e`/`-P` 的传统加密强度弱，真需要保密请用 `7z` 的 AES-256 或 `gpg` 加密外层文件。

## 相关命令

- `unzip` — 列出、校验、解压 zip
- `tar` — `tar -czf`/`-cJf`，单流压缩率更高
- `zipnote` / `zipgrep` / `zipinfo` — 查看注释、在归档内搜索、看详细信息
- `7z` — 同一归档格式，支持更强加密与更新
