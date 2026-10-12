// reactbits 组件大多不自带 reduced-motion 判断,统一在这一层兜住。
const query = matchMedia('(prefers-reduced-motion: reduce)');

export const reduceMotion = query.matches;
