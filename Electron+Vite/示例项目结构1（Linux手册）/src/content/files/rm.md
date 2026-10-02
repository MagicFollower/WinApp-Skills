# rm — 删除文件与目录

> 删除不可恢复；`-r` 递归删目录，`-f` 忽略不存在的文件并去掉询问，`-I` 只批量确认一次。

## 语法
```bash
rm [OPTION]... [FILE]...
```

## 常用选项
| 选项 | 说明 |
| --- | --- |
| `-f`, `--force` | 文件不存在也不报错，全程不询问 |
| `-i` | 每个文件都询问 |
| `-I` | 只在删除超过 3 个文件或递归删除时询问一次 |
| `-r`, `-R`, `--recursive` | 递归删除目录及其内容 |
| `-d`, `--dir` | 删除空目录（等价于 `rmdir`） |
| `-v` | 逐个打印删除结果 |
| `--preserve-root` | 默认开启，拒绝递归删除 `/` |
| `--no-preserve-root` | 关闭上述保护，可递归删除 `/` |
| `--one-file-system` | 配合 `-r`，不跨越挂载点 |

## 示例
删除构建产物，目标不存在也不报错：

```bash
rm -f /tmp/build/*.o
```

递归清掉项目里的两个输出目录，用相对路径降低风险：

```bash
cd ~/work/myapp
rm -rf ./build ./dist
```

一次确认即可批量删除，比 `-i` 少刷屏：

```bash
rm -I ~/Downloads/*.zip
```

删除以 `-` 开头的诡异文件名，用 `./` 或 `--` 终止选项解析：

```bash
rm -- ./-data.txt
rm -f ./--log
```

按条件删除，比 `rm` 加通配符更可控：

```bash
find /var/tmp -type f -mtime +30 -delete
```

需要可恢复时改用回收站而不是 `rm`：

```bash
gio trash old-photo.png
```

## 注意事项
- `rm` 只删除目录项；若仍有进程持有打开的文件描述符，磁盘空间不会立即释放，用 `lsof +L1` 查看这类文件。
- `sudo rm -rf "$DIR"/*` 里 `$DIR` 为空或未定义时可能变成删根，脚本中先写 `${DIR:?}` 强制校验，并避免在 `/` 附近使用通配符。
- shell 通配符不匹配隐藏文件，`rm -rf dir/*` 会留下 `.file`；要清空整个目录就直接 `rm -rf dir`。
- 有 `immutable` 属性（`chattr +i`）或只读挂载上的文件会报 `Operation not permitted`，需先 `chattr -i` 或重新挂载。
- 删除符号链接只删链接本身，不会碰到它指向的内容。

## 相关命令
- `rmdir` — 只删空目录，误删风险更低
- `find ... -delete` — 按条件精确删除
- `shred` — 覆写内容后删除，降低恢复可能
- `gio trash` / `trash-cli` — 移入回收站，可恢复
