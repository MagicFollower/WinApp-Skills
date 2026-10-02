# passwd — 修改口令与口令有效期

> 设置账号口令，并锁定、解锁、置空、强制过期；口令的生命周期规则由 `/etc/shadow` 与 `chage` 共同管理。

## 语法

```bash
passwd                       # 修改自己的口令（会先要求输入旧口令）
passwd [选项] [账号]         # root 可操作任意账号
passwd -S 账号               # 查看口令状态
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-l`, `--lock` | 在 `/etc/shadow` 的口令字段前加 `!`，口令认证失效 |
| `-u`, `--unlock` | 去掉 `!` 前缀恢复可用 |
| `-d`, `--delete` | 清空口令字段，账号变成「无口令」而非「锁定」 |
| `-e`, `--expire` | 把最后修改日期置为 0，下次登录强制改口令 |
| `-S`, `--status` | 输出账号名、状态、最后修改日期与各项天数 |
| `-n`, `-x`, `-w`, `-i` | 最短使用天数 / 最长使用天数 / 到期警告天数 / 过期后宽限天数 |
| `-p`, `--password` | 直接写入已加密的口令串（会出现在进程列表，慎用） |
| `-k`, `--keep-tokens` | 只更新已过期的认证令牌 |
| `--stdin` | 从标准输入读明文口令，仅 RHEL/SUSE 系的 shadow 提供 |

## 示例

普通用户改自己的口令会先要求旧口令；root 可以直接为服务账号设置，并强制其下次登录修改：

```bash
passwd
sudo passwd deploy
sudo passwd -e deploy
```

状态与有效期一起看，第二条把天数集中写清：

```bash
sudo passwd -S deploy
# deploy P 2026-06-01 7 90 14 -1 (Password set, SHA512 crypt.)
sudo chage -M 90 -m 7 -W 14 deploy
sudo chage -l deploy
```

交接或离职处置，锁定口令同时限制登录 shell 与失效日期：

```bash
sudo passwd -l bob
sudo usermod -L -s /usr/sbin/nologin -e 1970-01-01 bob
sudo pkill -u bob
```

批量建号时用管道喂明文，避免出现在 `ps` 与历史里：

```bash
printf 'deploy:%s\n' "$DEPLOY_PW" | sudo chpasswd
```

## 注意事项

- `-d` 与 `-l` 完全不同：字段被清空后，只要 PAM 允许空口令（`pam_unix nullok`），本地 tty 就能免口令登录；服务账号一律用 `-l` 或 `usermod -L`。
- 锁定只影响口令认证。`~/.ssh/authorized_keys`、`sudo` 已有的免密策略、cron 与正在运行的进程都不受影响，彻底停用需要「锁口令 + 改 nologin + 清 authorized_keys + 杀会话」四步。
- `passwd -S` 的第三个字段是最后修改日期，`chage -l` 显示得更完整；两者都只改 `/etc/shadow`，`/etc/passwd` 里看不到口令。
- 复杂度与历史记录由 PAM 决定（`pam_pwquality`、`pam_unix remember`），`/etc/login.defs` 的 `PASS_MAX_DAYS`、`PASS_MIN_LEN` 只对新账号和默认值生效；root 设置弱口令时只会提示确认而不阻止。

## 相关命令

- `chage` — 只调有效期与失效日期，不动口令
- `chpasswd` — 从标准输入批量设置口令
- `usermod` — `-L`/`-U`、改 shell、改过期日期
- `useradd` — 建号时一并设定 `-e`、`-f`
- `faillock` / `pam_tally2` — 查看失败计数是否把账号锁住
