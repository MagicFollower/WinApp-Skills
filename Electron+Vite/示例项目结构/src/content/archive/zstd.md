# zstd — 速度与压缩率的均衡点

> zstd（Zstandard）在接近 gzip 的耗时里给出远好于 gzip 的压缩率，`-1`~`-19` 为常规级别、`--ultra` 解锁更高级别，并能递归处理目录。

## 语法

```bash
zstd [选项] [文件...]
zstd -d 文件.zst          # 解压；unzstd 是其别名
zstdcat 文件.zst          # 等于 zstd -dc，只看内容不落地
tar --zstd -cf app.tar.zst app/
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-1` … `-19` | 压缩级别，默认 `-3`；越高越慢、越小 |
| `--ultra -22` | 解锁 `-20`~`-22`，用于一次成型、长期存放的归档 |
| `-d`, `--decompress` | 解压；`unzstd` 等价 |
| `-k`, `--keep` / `--rm` | zstd 默认保留输入文件，`--rm` 才删除（向 gzip 行为看齐） |
| `-o 文件` | 指定输出文件；`-c`/`--stdout` 输出到标准输出 |
| `-r` | 递归处理目录，目录内每个文件各自生成 `.zst` |
| `-T`, `--threads=N` | 多线程压缩，`-T0` 按核心数自动；`zstdmt` 即 `-T0` 别名 |
| `--long[=BITS]` | 加大窗口（如 `--long=27`），大文件中相距很远的内容也能互相供词典 |
| `-t`, `--test` | 校验 `.zst` 完整性 |
| `-l`, `--list` | 列出 `.zst` 文件的压缩前后大小等信息 |
| `-D 字典` / `--train` | 使用训练出的字典 / 从样本训练字典 |

## 示例

压缩单个文件；默认级别已经很快，长期归档再上 `-19 --long`：

```bash
zstd -k app.log                        # 生成 app.log.zst，app.log 保留
zstd --ultra -22 --long=27 -k vm.img
```

配合 tar：`--zstd` 需要 GNU tar 1.31 及以上并且系统里有 `zstd`；不确定时走管道最稳：

```bash
tar --zstd -cf app-1.24.0.tar.zst -C /srv app
tar -cf - -C /srv app | zstd -T0 -19 > app-1.24.0.tar.zst
```

查看、校验并解压，`-o` 可改回原来的文件名；`zstdcat` 直接接给 tar 或 grep：

```bash
zstd -l app-1.24.0.tar.zst && zstd -t app-1.24.0.tar.zst
zstd -d app-1.24.0.tar.zst -o app.tar
zstdcat app-1.24.0.tar.zst | tar -tf - | head -20
```

递归压缩整个日志目录；大量结构相似的小文件用字典能显著提高压缩率：

```bash
zstd -r -k -19 /var/log/app
zstd --train /var/log/app/*.sample -o app.dict
zstd -D app.dict -k small.log         # 解压同样要 -D app.dict
```

## 注意事项

- zstd 默认**不删除**源文件（与 gzip/bzip2/xz 相反），要省空间必须显式 `--rm`；覆盖已存在的 `.zst` 需要 `-f`，脚本里建议显式 `-o` 或 `-c >`。
- 用 `--ultra` 高级别或 `--long` 压出的文件需要较新的 zstd 才能解压，接收方版本过旧会报窗口/字典相关错误；对外分发时保守选 `-19`。
- `-T` 多线程只明显加速压缩，单个帧的解压基本是单线程；超大归档建议分段写入多帧。
- `-r` 给目录里每个文件单独生成 `.zst`，小文件多时压缩率低，先 `tar` 成一个流再压更划算。

## 相关命令

- `tar` — `--zstd` 或管道方式生成 `.tar.zst`
- `gzip` — 兼容性最好，但速度与压缩率都不占优
- `xz` — 极致体积、可接受慢速时仍然更强
- `lz4` — 同族里的极端速度档，压缩率最低
