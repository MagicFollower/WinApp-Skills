# cd — 切换当前工作目录

> `cd` 是 shell 内建命令，改变的是当前 shell 的工作目录；`-P` 会解析符号链接。

## 语法
```bash
cd [-L|-P [-e]] [dir]
```

## 常用选项
| 选项/写法 | 说明 |
| --- | --- |
| `-L` | 逻辑切换，`$PWD` 中保留符号链接名（bash 默认） |
| `-P` | 物理切换，得到不含符号链接的真实路径 |
| `-e` | 配合 `-P`，无法确定新目录时返回非 0（bash 扩展） |
| 无参数 | 回到 `$HOME` |
| `cd -` | 回到上一个目录 `$OLDPWD`，并打印新路径 |
| `cd ~user` | 进入指定用户的主目录 |
| `cd +N` / `cd -N` | 按目录栈位置跳转（bash） |

## 示例
进入再回到上一个目录：

```bash
cd /etc/systemd/system
cd -
```

`/var/run` 常是指向 `/run` 的符号链接，对比两种切换结果：

```bash
cd /var/run
echo "$PWD"
cd -P /var/run
pwd -P
```

脚本里切换失败就立刻退出，避免在错误目录继续执行：

```bash
cd /srv/app || exit 1
```

设置 `CDPATH` 后可从任意位置直接跳进项目目录：

```bash
export CDPATH=".:$HOME/projects"
cd myapp
```

用目录栈在多个目录之间来回切换：

```bash
pushd /var/log > /dev/null
pushd /etc > /dev/null
dirs -v
popd
```

## 注意事项
- `cd` 只影响当前 shell：`(cd /tmp; ls)` 或 `bash -c 'cd /tmp'` 不会改变父 shell 的目录。
- 进入目录需要路径每一层的执行（`x`）权限，缺一层就报 `Permission denied`。
- `cd -` 依赖 `$OLDPWD`，只有在同一个 shell 里先 `cd` 过才有效；脚本中建议写 `cd "$dir" || return`。
- `CDPATH` 会让 `cd foo` 命中别处的同名目录，写脚本时可先 `unset CDPATH` 保证只按相对路径解析。

## 相关命令
- `pwd` — 打印当前目录的绝对路径
- `pushd`, `popd`, `dirs` — 目录栈跳转
- `readlink` — 查看符号链接指向的真实目标
