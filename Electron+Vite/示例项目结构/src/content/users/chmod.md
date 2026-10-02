# chmod — 用符号模式与八进制模式设置权限

> 修改文件或目录的属主/属组/其他人读写执行位，支持递归、大写 `X` 语义与 setuid/setgid/sticky 位。

## 语法

```bash
chmod [选项] <符号模式> <路径>...
chmod [选项] <八进制模式> <路径>...
chmod [选项] --reference=模板文件 <路径>...
```

符号模式形如 `[ugoa][+-=][rwxXst]`，可用逗号串联多组；八进制模式形如 `[0-7][0-7][0-7][0-7]`，最高位依次是 setuid(4)、setgid(2)、sticky(1)。

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-R`, `--recursive` | 递归处理目录树里的每个条目 |
| `-c`, `--changes` | 只在权限确实变化时输出，比 `-v` 安静 |
| `-v`, `--verbose` | 每个文件都打印一条结果 |
| `-f`, `--silent` | 屏蔽「文件不存在/无权限」之类错误 |
| `--reference=RFILE` | 复制 `RFILE` 的权限位，不再写模式 |
| `-H` / `-L` / `-P` | 递归时跟随符号链接：仅命令行项 / 全部 / 都不跟随（默认） |

大写 `X` 与 `x` 的区别：`X` 只在目标是目录、或该文件已有任一类执行位时才加执行位，普通数据文件保持不带 `x`。

## 示例

新建脚本一般是 `0644`，给属主加执行位后变成 `-rwxr--r--`，即 `0744`。

```bash
ls -l /srv/scripts/nightly-backup.sh      # -rw-r--r--
chmod u+x /srv/scripts/nightly-backup.sh  # -rwxr--r-- = 0744
```

应用目录常用组合：目录 `0750` 让组可进入，普通文件 `0640` 让组可读，其他人无权限。

```bash
chown -R appuser:appgroup /srv/app
find /srv/app -type d -exec chmod 0750 {} +
find /srv/app -type f -exec chmod 0640 {} +
```

用大写 `X` 一次性放开「可读/可进入」，避免把上传的文件变成可执行；已有执行位的脚本会被保留。

```bash
chmod -R a+rX,u+w,go-w /srv/app/htdocs
# 目录 -> drwxr-xr-x (0755)，普通文件 -> -rw-r--r-- (0644)
```

共享上传目录需要其他人可写但彼此不能删除，用 sticky 位 `1777`；setgid 目录 `2775` 能让新建文件继承所属组。

```bash
chgrp www-data /srv/app/uploads && chmod 2775 /srv/app/uploads  # drwxrwsr-x
chmod 1777 /srv/app/tmp                                         # drwxrwxrwt
```

## 注意事项

- 只有文件属主或 root 能执行 `chmod`；改组要属主是 root，或属主对文件有写权限且目标组是其成员之一。`-R` 默认不跟随遍历中遇到的符号链接，要改链接指向的目标需显式加 `-L`，对 `/` 的递归则由 `--preserve-root` 默认拦截。
- `chmod` 不影响新建文件的默认权限，那由 `umask` 决定：`022` → 文件 `0644`、目录 `0755`；`002` → `0664` / `0775`；`077` → `0600` / `0700`。
- 文件带 ACL 时，`chmod` 改的是 mask 行，`ls -l` 显示的组权限可能与 `getfacl` 里的 group 条目不一致，先用 `getfacl` 确认。
- Linux 忽略 shebang 脚本上的 setuid 位，`chmod 4755 script.sh` 不会真正生效；二进制工具还需确认路径属 root 所有。

## 相关命令

- `chown` — 修改属主与所属组
- `chgrp` — 只改所属组
- `umask` — 查看或设置新建文件的默认掩码
- `setfacl` / `getfacl` — chmod 三位表达不了的细粒度授权
- `install` — 复制文件的同时设定权限与属主，适合部署脚本
