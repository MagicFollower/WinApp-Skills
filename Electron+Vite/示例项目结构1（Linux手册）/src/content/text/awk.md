# awk — 按列处理文本、条件筛选与统计

> 把每一行按 `FS` 切成字段再执行程序，天然适合日志、CSV 这类结构化文本的过滤与汇总。

## 语法

```bash
awk [选项] '程序' 文件...
awk [选项] -f 程序文件 文件...
awk [选项] '程序' < 文件
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-F 分隔符` | 设置 `FS`，如 `-F,`、`-F '\t'`；两个以上字符时按正则解释 |
| `-v name=value` | 在 `BEGIN` 之前就完成赋值，可重复使用传入参数 |
| `-f 程序文件` | 从文件读取 awk 程序，避开 shell 引号转义 |
| `--source '程序'` | 在命令行上分段给出程序，可与 `-f` 交错使用，长脚本更好读 |
| `--` | 选项结束标志，程序或文件名以 `-` 开头时使用 |

## 示例

`$9` 是状态码，条件为真才执行后面的动作块，`NR` 给出行号：

```bash
awk '$9 >= 500 {print NR, $1, $7, $9}' /var/log/nginx/access.log
```

`$NF` 是最后一列，列数不确定时比写死下标可靠：

```bash
awk '{print $1, $NF}' build_report.tsv
```

CSV 跳过表头做求和与平均，`BEGIN` 在读文件前执行，`END` 在读完后执行：

```bash
awk -F, 'NR>1 {sum += $3; n++} END {printf "rows=%d total=%.2f avg=%.2f\n", n, sum, sum/n}' sales.csv
```

用关联数组按来源 IP 计数，再交给 `sort` 排名：

```bash
awk '{cnt[$1]++} END {for (ip in cnt) print cnt[ip], ip}' access.log | sort -nr | head
```

`NR==FNR` 是只处理第一个文件的经典写法，用来做"排除名单"过滤：

```bash
awk -F, 'NR==FNR {bad[$1] = 1; next} !($1 in bad)' blacklist.csv all_users.csv
```

## 注意事项

- 字段从 `$1` 开始，`$0` 是整行；`NR` 跨文件累加计数，要看文件内行号用 `FNR`。
- 默认 `FS` 是空白：行首行尾空格被忽略、连续空白算一个分隔符；`-F,` 只按逗号切，遇到内含逗号的带引号 CSV 字段会切错。
- 引用超出 `NF` 的字段得到空串，同时会把这个字段创建出来并改变 `NF`，稳妥做法是先判断 `NF >= 7`。
- 数值直接 `print` 时按 `OFMT`（默认 `%.6g`）格式化，金额请改用 `printf` 指定精度；想以 `OFS` 重新拼接整行要写 `$1 = $1`。

## 相关命令

- `cut` — 只按固定分隔符取列时更简单
- `sed` — 只做文本替换不必上 awk
- `sort` 与 `uniq` — awk 汇总后排序计数
