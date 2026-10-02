# su — 切换用户并启动（登录）shell

> 用目标账号的口令切换到另一个用户；加 `-` 得到干净的登录环境，`-c` 只跑一条命令。

## 语法

```bash
su [选项] [-] [账号 [参数...]]
su [选项] -c '<命令>' [账号]
```

不带账号时默认切换到 root；账号之后的参数会原样交给目标 shell，所以 `-c` 的命令要用引号包住，单独的 `-` 只有写在账号之前才表示登录 shell。

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-`, `-l`, `--login` | 完整登录 shell：重读环境、`cd` 到目标家目录、按目标账号设置 `HOME`/`PATH`/`SHELL`/`USER` |
| `-c`, `--command=命令` | 交给目标账号的 shell 以 `-c` 执行一次，不进入交互 |
| `-s`, `--shell=SHELL` | 指定要启动的 shell，覆盖 `/etc/passwd` 中的记录 |
| `-p`, `-m`, `--preserve-environment` | 保留当前环境（与 `-` 冲突），非 root 使用受限 |
| `-g 组` / `-G 组` | root 切换时指定主组 / 追加补充组（`-G` 可重复给出） |
| `-w`, `--whitelist-env-list 变量表` | 较新版本 shadow 的 `su` 才支持，白名单方式保留指定环境变量 |

## 示例

临时进入服务账号的干净环境排障：

```bash
su - appuser
pwd        # /home/appuser
id -un     # appuser
```

只跑一条命令、不落地交互 shell（引号要成对，内层用单引号）：

```bash
su - postgres -c "psql -c 'VACUUM ANALYZE;'"
```

不加 `-` 时环境是「半新半旧」的，容易被误判身份：

```bash
su appuser -c 'echo "env USER=$USER"; whoami'
# $USER 可能仍是切换前的账号，身份以 whoami / id -un 为准
```

账号 shell 是 `nologin`/`git-shell` 时，root 可临时指定可用 shell：

```bash
su -s /bin/bash - git -c 'id -Gn'
```

切到 root 做需要登录环境的批量操作（需 root 口令）：

```bash
su - -c 'systemctl daemon-reload && systemctl restart nginx'
```

## 注意事项

- `su` 要的是**目标账号**的口令；Ubuntu 等默认不给 root 设口令的发行版会直接失败，改用 `sudo -i`。非登录模式只保留少数环境变量（`TERM`、`SHELL`、`HOME` 等），`PATH` 由 `/etc/login.defs` 的 `ENV_PATH` / `ENV_ROOT_PATH` 决定，判断身份别看 `$USER`，用 `id`。
- 目标是 `nologin`/`false` 这类 shell 时，直接 `su 账号` 会报「account is currently not available」，需 root 用 `-s` 指定真实 shell；非 root 用 `-s` 时目标 shell 通常需在 `/etc/shells` 中登记。
- 共享 root 口令意味着无法追责、也难以撤销；日常管理优先 `sudo -u 账号`，`su` 留给救援模式与确实需要整会话的场景。
- 可通过 `/etc/pam.d/su` 里的 `pam_wheel.so use_uid` 或 `login.defs` 的 `SU_WHEEL_ONLY` 限制谁能 `su` 到 root。

## 相关命令

- `sudo -i` / `sudo -u 账号 -s` — 用自己口令进入他人环境并留审计
- `sudo -u 账号 -c` — 一次性以某账号执行命令
- `id` / `whoami` — 确认切换后的真实凭证
- `newgrp` — 只切主组，不动用户
