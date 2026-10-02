# unzip — 解压与校验 zip

> unzip 列出、测试并解压 `.zip`，通过 `-n`/`-o` 控制同名文件的覆盖策略，`-d` 指定落地目录。

## 语法

```bash
unzip [选项] 归档.zip [成员...] -x [排除...] -d 目标目录
unzip -l 归档.zip        # 列出
unzip -t 归档.zip        # 校验
unzip -q 归档.zip -d /opt/app
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-l` | 只列出成员名与大小，不写盘 |
| `-t` | 逐个解压到内存做 CRC 校验，不产生文件 |
| `-p` | 把指定成员解压到标准输出 |
| `-n` | 不覆盖已存在的文件（默认行为会询问） |
| `-o` | 覆盖已存在的文件且不再询问，同时不重建目录提示 |
| `-q` | 静默，不打印每个文件的处理过程 |
| `-d 目录` | 解压到该目录，目录不存在会自动创建 |
| `-j` | 丢弃路径，把所有成员平铺到目标目录 |
| `-x 模式` | 排除成员，模式需引号包裹 |
| `-P 密码` | 命令行给密码（会出现在进程列表），留空则会交互提示 |
| `-O 字符集` | 文件名编码转换，如 `-O CP936`；**并非所有发行版的构建都启用了该选项**，未启用时报无效选项 |

## 示例

先看清楚结构再决定解压到哪里，避免整棵树直接摊在当前目录：

```bash
unzip -l release.zip | head -30
unzip -t release.zip
```

常规解压到指定目录，静默并覆盖同名文件：

```bash
unzip -qo release.zip -d /opt/app
```

只取其中一个成员、直接看内容不落盘，或排除无关目录后平铺取出：

```bash
unzip -qo release.zip "app/config/app.yaml" -d /tmp/inspect
unzip -p release.zip app/README.md | head -40
unzip -qo bundle.zip -j -x "__MACOSX/*" "*.DS_Store" -d /tmp/assets
```

Windows 下打包的中文名出现乱码时，先试 `-O`，不支持就改用 unar 或事后用 convmv 转名：

```bash
unzip -O CP936 -q 资料.zip -d /tmp/out   # 需构建时启用字符集转换
unar -O CP936 资料.zip                    # 备选：自动探测编码
convmv -f gbk -t utf8 --notest /tmp/out   # 先看转换结果，再去 --notest 执行
```

## 注意事项

- 默认遇到同名文件会逐个询问，无人值守脚本必须显式给 `-n`、`-o` 或 `-u`，否则会卡住。
- 归档内部路径可能含 `../` 或绝对路径，解压前用 `-l` 检查；这类成员写到目标目录之外，unzip 会告警但仍可能落地。
- `-O`/`-I` 等编码转换选项依赖编译开关，Debian/Ubuntu 的官方包通常没有启用，跨平台归档优先使用 `unar` 或让对方用 UTF-8 标志位打包。
- `unzip` 退出码非 0 即表示校验失败、参数有误或没有匹配成员；批量脚本中要逐包判断，不要只看最后一条命令。

## 相关命令

- `zip` — 创建与更新归档；`zip -T` 同样能校验
- `zipinfo` — 与 `unzip -l/-v` 等信息等价的独立程序
- `7z` — 可读写 zip，并支持更多压缩与加密格式
- `tar` — 处理 `.tar.gz` 等非 zip 归档
