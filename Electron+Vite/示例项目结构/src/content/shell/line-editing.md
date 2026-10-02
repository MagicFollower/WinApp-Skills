# 行编辑键（readline）

> Bash 交互命令行默认走 readline 的 emacs 模式，光标移动、剪切与历史检索都由控制键驱动。

## 要点

```bash
bind -p        # 打印按键与 readline 函数的绑定表
bind -v        # 打印 readline 变量（bell-style、completion-ignore-case 等）
bind '"\e[A": history-search-backward'   # 上箭头改为按已输入前缀检索历史
set -o vi      # 切到 vi 编辑模式；set -o emacs 切回默认
```

## 按键

| 名称 | 说明 |
| --- | --- |
| `Ctrl+A` / `Ctrl+E` | 跳到行首 / 行末（`beginning-of-line`、`end-of-line`） |
| `Ctrl+B` / `Ctrl+F` | 左移 / 右移一个字符，等价于左右方向键 |
| `Alt+B` / `Alt+F` | 左移 / 右移一个单词 |
| `Ctrl+U` / `Ctrl+K` | 剪切光标到行首（`unix-line-discard`）/ 到行末（`kill-line`） |
| `Ctrl+W` / `Alt+D` | 按空白边界向前删词（`unix-word-rubout`）/ 向后删词（`kill-word`） |
| `Ctrl+Y` | 粘贴 kill-ring 顶部内容；接着按 `Alt+Y` 可轮换更早的剪切项 |
| `Ctrl+R` | 反向增量检索历史，再按一次继续往更早的方向找同一匹配 |
| `Ctrl+S` | 正向增量检索，常被终端 XON/XOFF 流控截获，需先 `stty -ixon` |
| `Ctrl+X Ctrl+X` | 交换光标与 mark，在两个位置间往返；`Ctrl+Space` 用来设 mark |
| `Ctrl+L` | 清屏并重画当前命令行，已输入内容不丢 |
| `Ctrl+D` | 删除光标处字符；空行上按下即 EOF，退出 shell |
| `Ctrl+X Ctrl+E` | 在 `$EDITOR`/`$VISUAL` 里编辑当前行（内部走 `fc`），适合多行命令 |
| `Ctrl+X Ctrl+R` | 重新读取 `~/.inputrc`（`re-read-init-file`），改完绑定不必重开终端；`Ctrl+/` 撤销本次编辑 |

## 示例

长命令发现开头漏了 `sudo`：`Ctrl+A` 回到行首补上，再 `Ctrl+E` 回到行末继续输入。

```bash
# 光标原本停在行末
sudo apt-get install nginx
```

把一段路径搬到另一条命令里：`Alt+B` 回退一个词，`Ctrl+W` 删掉光标前的整个词，`Ctrl+Y` 再粘到别处。

```bash
cp /etc/nginx/nginx.conf /tmp
# 光标在行末时 Ctrl+W 删掉的是 /tmp；先 Alt+B 回到路径词末尾再 Ctrl+W
tar czf backup.tar.gz
# 这里按 Ctrl+Y，刚剪切的 /etc/nginx/nginx.conf 会被粘贴进来
```

自定义绑定写进 `~/.inputrc`，再用 `Ctrl+X Ctrl+R` 生效（`Ctrl+R` 检索到的行要按回车才执行，`Ctrl+G` 放弃）：

```bash
set completion-ignore-case on
set history-preserve-point on
"\e[1;5C": forward-word
"\e[1;5D": backward-word
```

## 注意事项

- 这些键只属于交互式 readline；脚本里的 stdin 不是 tty，绑定一律无效，非交互执行要用 `read` 或 `getopts` 自己解析。
- `Alt+B`/`Alt+F` 要求终端把 Meta 键发成 `ESC` 前缀，部分 Windows 终端与串口控制台需在设置里改键位映射。
- `Ctrl+U`、`Ctrl+K` 剪切的内容共享同一个 kill-ring，`Ctrl+W` 同样进入该环；`IGNOREEOF` 可让空行 `Ctrl+D` 不退出 shell。
- `Ctrl+R` 只是把命中的历史取到命令行，回车才执行；此时按 `Ctrl+C` 会放弃整行，需要重新检索。

## 相关命令

- `bind` — 查看与修改 readline 绑定和变量
- `fc` — 把历史命令交给编辑器批量修改
- `history` — 列出、检索、清理命令历史
- `stty` — 查看终端控制字符，`stty -ixon` 释放 `Ctrl+S`/`Ctrl+Q`
- `set` — `set -o emacs` / `set -o vi` 切换整套编辑模式
