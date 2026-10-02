# sudo — 以其他身份执行命令

> 用自己的口令验证后，按 `/etc/sudoers` 策略以 root 或指定账号运行命令，并留下审计记录。

## 语法

```bash
sudo [选项] [-u 账号[:组]] <命令> [参数...]
sudo -i | -s | -l | -k | -v
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-u`, `--user=账号[:组]` / `-g`, `--group=组` | 以该账号（可再指定组）运行，缺省为 root |
| `-i`, `--login` / `-s`, `--shell` | 启动目标账号的登录 shell（环境按登录重建）/ 只起 shell，不做登录重置 |
| `-E`, `--preserve-environment` | 保留当前环境，需策略授予 `setenv` 权限 |
| `-l`, `--list` / `-n`, `--non-interactive` | 列出允许与拒绝的命令（`-ll` 更详细）/ 需要口令时直接失败，适合脚本与 cron |
| `-k` / `-K` / `-v` | 使口令缓存失效 / 彻底删除缓存记录 / 只刷新缓存并验证 |
| `-e`, `--edit` | 以 `sudoedit` 方式编辑受保护文件 |
| `-S` / `-b` / `-p` | 从标准输入读口令 / 后台运行 / 自定义提示串 |

## 示例

以其他账号跑一条命令不需要对方口令；`-E` 把当前环境带过去，前提是策略允许：

```bash
sudo -u postgres psql -c 'ANALYZE;'
sudo -E -u appuser env > /tmp/app.env
```

需要完整 root 环境时用登录 shell 而不是 `sudo su -`，脚本里用 `-n` 避免卡在口令提示：

```bash
sudo -i
sudo -n systemctl restart myapp.service || echo "策略未放行或需要口令"
```

先确认当前身份被授予了什么：

```bash
sudo -l
sudo -ll -U deploy        # 查看别人的授权需要 root
```

最小化授权放在 `/etc/sudoers.d/` 的独立文件里，用 `visudo -f` 编辑才有语法校验：

```bash
sudo visudo -f /etc/sudoers.d/deploy
```

```
Cmnd_Alias APPCTL = /usr/bin/systemctl restart myapp.service, \
                    /usr/bin/systemctl status myapp.service
deploy  ALL=(appuser:appgroup) NOPASSWD: APPCTL
Defaults:deploy timestamp_timeout=3
```

## 注意事项

- 验证用的是你自己的口令（账号需在 `sudo`/`wheel` 组内或被策略显式授权）；缓存默认 5 分钟、按 tty 分别计算，记录在 `/var/run/sudo/ts/`，`timestamp_timeout=0` 每次询问、`-1` 会话内不再询问，离开键盘前先 `sudo -k`。
- 默认 `env_reset` 会清洗环境，设置了 `secure_path` 后连 `PATH` 也被覆盖；没有 `setenv` 权限时 `-E` 被静默忽略。
- 通配符加上「能起子 shell 的命令」是主要风险：`/usr/bin/less *`、`vim`、`find -exec` 一旦授权就近似 root，应写绝对路径并固定参数。
- `/etc/sudoers` 与 `/etc/sudoers.d/*` 必须 `root:root 0440`，文件名含 `.` 或以 `~` 结尾会被跳过；保存前 `visudo -c`，否则语法错误会把所有人锁在门外。

## 相关命令

- `su` — 用目标账号口令切换，适合没有 sudo 策略的救援场景
- `sudoedit` — 编辑受保护文件而不给对方一个 root shell
- `visudo` — 带语法校验地编辑策略
- `id` / `groups` — 确认账号在不在 sudo/wheel 组里
