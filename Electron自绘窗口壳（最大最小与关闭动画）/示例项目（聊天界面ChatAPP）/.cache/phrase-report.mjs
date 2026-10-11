// 读 --phrase 探针的日志,只打判据字段(整行 JSON 里 labels 有 120 项,直接看会淹掉)。
// 用法: node .cache/phrase-report.mjs .cache/phrase.log
import fs from 'node:fs';

const raw = fs.readFileSync(process.argv[2], 'utf8');
const line = raw.split('\n').find((l) => l.startsWith('phrase '));
if (!line) {
  console.log('no phrase line; head was:\n' + raw.slice(0, 400));
  process.exit(1);
}
const j = JSON.parse(line.slice('phrase '.length));
const keep = (o) => {
  if (!o || typeof o !== 'object') return o;
  const { labels, ...rest } = o;
  return { ...rest, nLabels: labels ? labels.length / 3 : 0 };
};
for (const k of [
  'before', 'tap', 'afterTap', 'hold', 'afterHold', 'early', 'earlyRelease',
  'rightClickAt', 'afterRightClick', 'afterRename', 'collapse', 'afterCollapse',
  'expand', 'afterExpand', 'addN', 'afterFill', 'panelH'
]) {
  console.log(k.padEnd(16), JSON.stringify(keep(j[k])));
}
