# mkdir — 创建目录

> `-p` 一次建出整条路径且对已存在目录静默成功，`-m` 直接指定权限。

## 语法
```bash
mkdir [OPTION]... DIRECTORY...
```

## 常用选项
| 选项 | 说明 |
| --- | --- |
| `-p`, `--parents` | 创建缺失的父目录；目标已存在也不报错 |
| `-m`, `--mode=MODE` | 用八进制或符号模式设定权限，不受 umask 削弱 |
| `-v`, `--verbose` | 打印每个被创建的目录 |
| `-Z` | 在启用 SELinux 的系统上指定安全上下文 |

## 示例
一次建出多级目录，并显示过程（花括号展开由 bash 提供）：

```bash
mkdir -pv ~/projects/app/{data,logs,cache}
```

团队共享目录，权限固定为 `rwxrwsr-x`（setgid 让新文件继承属组）：

```bash
sudo mkdir -m 2775 /srv/project
sudo chown :developers /srv/project
```

创建用户级配置目录，路径中已有同名目录也不会失败：

```bash
mkdir -p "$HOME/.config/myapp"
```

配合变量安全建目录，变量为空时立即报错：

```bash
mkdir -p "${DEPLOY_DIR:?未设置 DEPLOY_DIR}/releases/$(date +%F)"
```

末尾带斜杠的路径同样可用，`-p` 会忽略它：

```bash
mkdir -p /tmp/demo/
```

## 注意事项
- 默认权限是 `0777` 再扣除 umask，`umask 077` 时 `mkdir d` 得到 `700`；只有 `-m` 给出的模式不被 umask 影响。
- 需要父目录的写和执行权限；权限不足时 `-p` 也只会在失败的那一层报错并返回非 0。
- 目标已存在时不带 `-p` 会报 `File exists` 并返回非 0；带 `-p` 则成功返回，且不会修改已存在目录的权限。
- 路径中间某一层已存在但是普通文件时，`-p` 会报 `Not a directory` 并失败。
- 路径中的符号链接指向别处时，`-p` 会沿用该链接目标，注意不要建到意外位置。

## 相关命令
- `rmdir` — 删除空目录
- `install -d` — 建目录同时设定权限与属主
- `chmod` — 事后调整目录权限
- `touch` — 创建空文件或更新时间戳
