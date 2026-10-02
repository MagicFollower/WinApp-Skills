# pwd — 打印当前工作目录

> 输出当前目录的绝对路径；默认给逻辑路径，`-P` 给解析符号链接后的真实路径。

## 语法
```bash
pwd [-L|-P]
```

## 常用选项
| 选项 | 说明 |
| --- | --- |
| `-L`, `--logical` | 采用 `$PWD`，路径里可能仍含符号链接名（未指定 `-P` 时的行为） |
| `-P`, `--physical` | 用 `getcwd(3)` 取真实路径，不含任何符号链接名 |
| `--help` | 外部 `/bin/pwd` 的用法说明 |
| `--version` | 外部 `/bin/pwd` 的版本信息 |

## 示例
打印当前目录：

```bash
pwd
```

对比逻辑路径与物理路径，确认是否站在符号链接下：

```bash
cd /var/run
echo "$PWD"
pwd
pwd -P
```

脚本中先保存当前位置，处理完再回来：

```bash
orig=$(pwd -P)
cd /tmp || exit 1
make -C "$orig" clean
cd "$orig" || exit 1
```

把当前目录作为参数传给其他命令，注意加引号：

```bash
tar -C "$(pwd)" -czf "$HOME/backup-$(date +%F).tar.gz" .
```

与 `realpath` 对照，两者在无链接路径上结果一致：

```bash
pwd -P
realpath .
```

## 注意事项
- `$PWD` 由 shell 维护，手动赋值或经 `CDPATH` 跳转后可能与真实位置不符；要精确就用 `pwd -P`。
- 内建 `pwd` 与 `/bin/pwd` 在符号链接下输出可能不同，用 `type -a pwd` 可看出有几个实现。
- 解析路径需要每一层的执行（`x`）权限；当前目录或父目录被删除后可能报 `cannot determine current working directory`。
- 输出不带末尾斜杠，根目录是唯一的例外（输出 `/`）。

## 相关命令
- `cd` — 切换当前工作目录
- `realpath` — 规范化任意路径并解析符号链接
- `readlink` — 查看符号链接指向的目标
- `dirs` — 显示 bash 目录栈
