# chown — 变更文件属主与所属组

> 把文件或目录的所有权交给另一个账号/用户组，常在部署、迁移家目录、修复解压产物时使用。

## 语法

```bash
chown [选项] <属主>[:<组>] <路径>...
chown [选项] :<组> <路径>...
chown [选项] --reference=模板文件 <路径>...
```

`属主:组` 也可写成 `属主.组`；只写 `user` 或 `user:` 表示只改属主，`chown :group` / `chown .group` 表示只改组。

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-R`, `--recursive` | 递归改整棵目录树 |
| `-h`, `--no-dereference` | 改符号链接本身而不是它指向的目标（Linux 支持链接属主） |
| `-c`, `--changes` / `-v`, `--verbose` | 只报实际变化 / 每个文件都报 |
| `-f`, `--silent` | 屏蔽无法访问文件的报错 |
| `--from=旧属主[:旧组]` | 只改当前属主匹配上的条目，递归时防止误伤 |
| `--reference=RFILE` | 以 `RFILE` 的属主和组为模板 |
| `-H` / `-L` / `-P` | 递归时跟随命令行链接 / 所有链接 / 不跟随（默认） |

## 示例

部署目录整体交给服务账号，权限收紧到组可读：

```bash
sudo chown -R appuser:appgroup /srv/app
sudo chmod 0750 /srv/app && sudo chmod 0640 /srv/app/config/app.yml
```

只想改组、不动属主时省略属主部分：

```bash
sudo chown :deploy /var/www/html/releases/latest
```

递归转换时用 `--from` 限定「只改还属于 root 的文件」，重复执行也不会覆盖已交接的内容：

```bash
sudo chown -R --from=root:root jenkins:jenkins /var/lib/jenkins/workspace
```

链接本身归属正确，避免运维看到 `root` 持有的软链接不敢动：

```bash
sudo chown -h deploy:deploy /srv/app/current
```

SSH 授权文件必须属于该用户本人，否则 `StrictModes` 会拒绝公钥登录：

```bash
sudo chown alice:alice /home/alice/.ssh /home/alice/.ssh/authorized_keys
sudo chmod 0700 /home/alice/.ssh && sudo chmod 0600 /home/alice/.ssh/authorized_keys
```

## 注意事项

- 改属主只有 root 能做到；改组则属主自己也能改，但目标组必须是自己的成员组。
- 不带 `-R` 时命令行给出的符号链接会被跟随（指向不存在的链接除外）；带 `-R` 默认不跟随树中的链接，需要时用 `-L`。
- 数字 UID 与用户名歧义：`chown 1001 file` 按 UID 解析，账号不存在时留下「孤儿属主」，`ls -l` 直接显示数字，想强制按名字解析可写 `chown ./账号 file`；删账号后残留的同 UID 文件会被新建账号「继承」，删除前用 `find / -user alice` 与 `find / -nouser` 核对。
- 关键文件属主/权限错了会直接影响认证与提权：`/etc/shadow` 应为 `root:shadow 0640`（部分发行版 `0000`），`/etc/sudoers` 应为 `root:root 0440`。

## 相关命令

- `chgrp` — 只改所属组，普通用户也能对自己的文件用
- `chmod` — 设置权限位
- `id` — 确认目标账号的 uid/gid 与附加组
- `install` — 复制时直接指定属主、属组和权限
