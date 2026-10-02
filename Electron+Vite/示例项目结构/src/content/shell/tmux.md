# tmux — 会话分离与复用

> tmux 把终端会话托管在后台 server 进程里，SSH 断开程序照跑，随时重新接回；默认前缀 `Ctrl+b`。

## 要点

```bash
tmux new -s work              # 新建并进入名为 work 的会话（换成 -d 只后台创建）
tmux ls                       # 列出会话
tmux attach -t work           # 接回（多客户端共享同一画面）
tmux attach -d -t work        # 接回并踢掉其他客户端
```

## 按键 / 选项

先按一次前缀 `Ctrl+b`，再按功能键。

| 名称 | 说明 |
| --- | --- |
| `Ctrl+b d` | detach，会话转后台继续运行 |
| `Ctrl+b %` / `Ctrl+b "` | 左右分屏 / 上下分屏 |
| `Ctrl+b o` / `方向键` / `Ctrl+b {` / `}` | 轮换窗格 / 定向切换 / 前移 / 后移窗格 |
| `Ctrl+b z` / `Ctrl+b {` / `}` | 窗格放大还原 / 前移 / 后移窗格 |
| `Ctrl+b x` / `Ctrl+b !` | 关闭当前窗格（需确认）/ 把窗格拆成独立窗口 |
| `Ctrl+b c` / `n` / `p` / `0..9` | 新建窗口 / 下一个 / 上一个 / 按编号跳转 |
| `Ctrl+b ,` / `Ctrl+b $` | 重命名当前窗口 / 重命名会话 |
| `Ctrl+b [` / `Ctrl+b ]` | 进入 copy-mode 回看 / 粘贴缓冲区 |
| `Ctrl+b s` / `w` / `:` / `?` | 选会话 / 选窗口 / 命令行模式 / 列出全部绑定 |

## 示例

建会话、分离、分屏与布局（`Ctrl+b d` 分离，`Ctrl+b %` / `"` 分屏）：

```bash
tmux new -s deploy -d
tmux ls
tmux attach -t deploy
tmux split-window -h -t deploy
tmux select-layout -t deploy tiled
tmux new-window -t deploy -n logs
```

回看与脚本驱动：`Ctrl+b [` 进 copy-mode（`q` 退出），vi 键位需先设 `mode-keys`：

```bash
tmux set -g mode-keys vi
tmux capture-pane -p -S -500 > /tmp/scrollback.txt
tmux send-keys -t deploy 'make -j4' Enter
tmux capture-pane -pt deploy | tail -20
```

配置写 `~/.tmux.conf`，收尾时分清 window、session、server 三级：

```bash
printf '%s\n' 'set -g mouse on' 'set -g history-limit 50000' >> ~/.tmux.conf
tmux source-file ~/.tmux.conf
tmux kill-window -t deploy:1
tmux kill-session -t deploy
tmux kill-server
```

## 注意事项

- `kill-server` 会终止所有会话里正在运行的程序且不可恢复，其他共享同一 server 的会话也一起没；只关一个用 `kill-session -t`。
- 没有 server 时 `tmux ls` 报 `no server running on /tmp/tmux-UID/default` 并返回非 0，脚本判存要用 `tmux has-session -t name 2>/dev/null`。
- 开启 `set -g mouse on` 后终端原生选中被 tmux 接管，复制需按住 Shift，或进 copy-mode 选中后用 `tmux save-buffer` 交给 `xclip`/`wl-copy`。
- tmux 内 `$TERM` 应为 `screen-256color` 或 `tmux-256color`，直接透传外层 `xterm-256color` 会出现颜色与清屏异常。

## 相关命令

- `screen` — 同类老牌复用器，键位与窗口模型不同
- `ssh -t host 'tmux attach -d -t work || tmux new -s work'` — 一行接通或新建
- `tmux pipe-pane -O 'cat >> pane.log'` — 把窗格输出持续落盘留档
- `nohup` / `setsid` — 只需脱离终端、不需要交互回看时的轻量方案
