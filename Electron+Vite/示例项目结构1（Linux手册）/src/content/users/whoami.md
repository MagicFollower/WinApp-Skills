# whoami — 当前生效的用户名

> 输出当前进程有效 UID 对应的账号名，切换身份后用它确认「我现在到底是谁」。

## 语法

```bash
whoami
```

没有任何实用选项（只有 `--help` 与 `--version`），也不能像 `id` 那样查询其他账号。

## 常用选项

| 对比项 | 依据 | 说明 |
| --- | --- | --- |
| `whoami` | 进程的**有效 UID**（`geteuid`） | `su`/`sudo` 之后显示目标账号 |
| `id -un` | 同上 | 与 `whoami` 等价，且支持 `id -un alice` 查别人 |
| `logname` | utmp 登录记录 | 显示最初登录的账号，不受 `su`/`sudo` 影响 |
| `who -m` / `who am i` | utmp 中当前终端条目 | 附带 tty、登录时间与来源主机 |
| `echo $USER` | 环境变量 | 会随 `su`（非登录）继承下来，也可能被随意改写 |
| `id -r -u -n` | 真实 UID | setuid 场景下区分「谁启动的」与「以谁在跑」 |

## 示例

普通会话与提权后的对照：

```bash
whoami
sudo whoami                # root
sudo -u www-data whoami    # www-data
```

`su` 不带 `-` 时环境变量可能是旧的，身份判断要以 `whoami` 为准：

```bash
su appuser -c 'echo "USER=$USER"; whoami; logname'
# USER 可能仍是 root，whoami 是 appuser，logname 是当初登录的账号
```

判断这个终端是谁登录的（`who` 会忽略多余参数，所以 `who am i` 成立）：

```bash
who -m
who am i
```

部署脚本自检运行身份：

```bash
[[ "$(whoami)" == appuser ]] || { echo "请用 appuser 执行" >&2; exit 1; }
```

查服务进程实际使用的账号，而不是启动它的人：

```bash
ps -o user= -p "$(pgrep -o nginx)"
```

## 注意事项

- `whoami` 只反映用户身份，不含组信息；需要主组与附加组请用 `id` 或 `groups`。
- 有效 UID 在 NSS 中没有对应条目时（容器里常见未映射的数字 UID），`whoami` 可能报错或退回输出数字，脚本里改用 `id -u` 比较稳妥。
- `logname` 与 `who` 依赖 utmp/`/var/run/utmp`，在容器、systemd 服务或缺少登录记录的场景会输出空或报 `no utmp entry`。
- 不要把 `$USER`、`whoami` 的输出当作授权依据；真正的权限由 UID/GID 和能力决定，安全检查用 `id -u` 并结合 sudoers 策略。

## 相关命令

- `id` — UID/GID 与全部附加组
- `logname` — 最初的登录账号
- `who` / `w` — 当前登录会话、终端与来源
- `groups` — 只看组名
- `sudo -l` — 查看当前身份可提权到什么
