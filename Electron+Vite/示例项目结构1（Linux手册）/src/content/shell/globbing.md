# 通配符展开（globbing）

> shell 在命令执行前把模式替换成实际文件名列表，命令收到的是已经展开后的名字。

## 语法

```bash
echo *.log
echo app.{c,h,o}
ls src/**/test_*.py          # 需先 shopt -s globstar
```

## 常用选项

| 名称 | 说明 |
| --- | --- |
| `*` / `?` | 任意长度（含零个）字符 / 恰好一个字符，两者都不匹配以 `.` 开头的名字 |
| `[abc]` / `[a-z]` / `[!0-9]` / `[^0-9]` | 字符集与区间（随 `LC_COLLATE`），后两种写法都是取反 |
| `{a,b}` | 花括号展开，早于通配执行且不检查文件是否存在 |
| `{1..5}` `{01..10}` `{a..e}` | 花括号的数字/字母序列形式，可给步长 |
| `**` | 配合 `shopt -s globstar` 递归跨目录 |
| `+(p)` `*(p)` `?(p)` `@(p)` `!(p)` | 扩展 glob，需 `shopt -s extglob` |
| `shopt -s nullglob` / `failglob` | 无匹配时展开为空 / 无匹配时报错并让命令失败 |
| `shopt -s nocaseglob` / `dotglob` | 匹配忽略大小写 / 让 `*` 也匹配隐藏文件 |

## 示例

集合与区间按位置筛选，花括号展开则是纯文本组合、与文件是否存在无关，这是它与 `*` 的本质区别：

```bash
ls 2024-0[1-6]-*.log
rm backup.{zip,tar,tgz}      # 展开成三个参数，缺哪个就报哪个错
mkdir -p {src,docs}/{en,zh}
printf '%s\n' {a,b}.{txt,md}
```

递归匹配要先开 globstar；`**/` 只匹配目录，便于统计层级：

```bash
shopt -s globstar
printf '%s\n' **/*.md
du -sh **/ | tail
```

引号抑制展开，反斜杠转义单个元字符：

```bash
echo "*.log"
echo \*.log
ls ./*.png                   # ./ 前缀防止文件名以 - 开头被当成选项
```

无匹配时的三种行为对比，脚本里首选 nullglob；`extglob` 开启后可用 `!(p)` 取反整个模式：

```bash
printf '[%s]' *.xyz                  # 默认：原样输出 [.xyz]
shopt -s nullglob;  printf '[%s]' *.xyz; echo
shopt -u nullglob; shopt -s failglob; ls *.xyz; echo "返回 $?"
shopt -s extglob; mv !(*.jpg|*.png) archive/
```

## 注意事项

- 展开顺序为花括号 → `~` → 变量与命令替换 → 词分割 → 通配 → 去引号，因此写在双引号里的 `"$pat"` 不再匹配文件。
- 默认行为是无匹配时把模式原样传给命令，`rm *.tmp` 一个都不存在时会报 `No such file or directory`，脚本里先开 `nullglob` 或在循环内判 `[ -e "$f" ]`。
- 通配结果由 shell 分成独立字段，且不会再被词分割，所以 `for f in *.log; do wc -l "$f"; done` 对含空格的文件名同样安全。
- `[a-z]` 在 `LC_COLLATE=en_US.UTF-8` 下包含中间的大写字母，需要严格 ASCII 区间时先 `LC_ALL=C`。

## 相关命令

- `shopt` — globstar / nullglob / failglob / nocaseglob / extglob 的开关
- `printf '%s\n'` — 逐行打印展开结果，避免 `echo` 吃掉分隔
- `find -name` / `-iname` — 自己的匹配语法，且默认递归
- `ls -U` — 按目录原始顺序输出，跳过 `ls` 的重排序
