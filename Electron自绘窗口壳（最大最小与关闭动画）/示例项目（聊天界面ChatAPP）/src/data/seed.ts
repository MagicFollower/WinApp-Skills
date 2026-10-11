import type { Chat, Message } from '../types';

export const SEED_CHATS: Chat[] = [
  { id: 'c0', type: 'group', name: 'Q谈国际(新闻)', members: 2, qq: '515604586', avatar: '🔥', avatarBg: '#f5a623', themeId: 'warm' },
  { id: 'c1', type: 'group', name: '炸弹基地', members: 1, qq: '382910447', avatar: '💣', avatarBg: '#4a4a55', themeId: 'mint' },
  { id: 'c2', type: 'group', name: 'CODM开黑群', members: 95, qq: '1029384756', avatar: '🎮', avatarBg: '#5b6472', themeId: 'warm' },
  { id: 'c3', type: 'group', name: '蔚蓝档案⭐收容物之智能体群', members: 142, qq: '774512038', avatar: '🌸', avatarBg: '#d63384', themeId: 'mint' },
  { id: 'c4', type: 'group', name: '半自由群', members: 65, qq: '294857103', avatar: '🌓', avatarBg: '#3a3f4b', themeId: 'warm' },
  { id: 'c5', type: 'group', name: '蔚蓝档案肘子交流群✦', members: 182, qq: '918273645', avatar: '🍖', avatarBg: '#f5a623', themeId: 'mint' },
  { id: 'c6', type: 'group', name: '烟花 三角洲行动交流群①', members: 27, qq: '465522187', avatar: '🎆', avatarBg: '#8a5cf6', themeId: 'warm' },
  { id: 'c7', type: 'group', name: 'AI编程交流 AI中转站', members: 147, qq: '1357924680', avatar: '🤖', avatarBg: '#1e2430', themeId: 'mint' },
  { id: 'c8', type: 'personal', name: '东方树叶.G3', members: 119, qq: '802341567', avatar: '🍃', avatarBg: '#0fa3a3', themeId: 'warm' },
];

/** 除 c0(原型头部那条)之外均为演示用虚构号码;换真号只改上面这张表。 */
export function findChat(id: string): Chat {
  return SEED_CHATS.find((c) => c.id === id) ?? SEED_CHATS[0];
}

/** 顶部信息卡的副标题口径:个人没有"成员数"这说。左栏行仍按原型显示「N 成员」。 */
export function chatSubtitle(chat: Chat): string {
  return chat.type === 'personal' ? `QQ:${chat.qq}` : `${chat.members} 成员 QQ:${chat.qq}`;
}

export const SENDER_COLORS = [
  '#f5a623',
  '#2bb673',
  '#3b9df8',
  '#8a5cf6',
  '#e5533d',
  '#d63384',
  '#0fa3a3',
  '#7a8c2e',
];

export function senderColor(name: string): string {
  let h = 0;
  for (let i = 0; i < name.length; i++) h = (h * 31 + name.charCodeAt(i)) >>> 0;
  return SENDER_COLORS[h % SENDER_COLORS.length];
}

export const SEED_MESSAGES: Message[] = [
  {
    id: 'm1',
    chatId: 'c1',
    sender: '苦命打工喵',
    own: false,
    kind: 'text',
    text: '😉很多小生意不是不会做产品,而是卡在"不会设计、不会写文案、不会做短视频"。\n\n其实先不用把 AI 想得太复杂。给 NanoGPTBot 一张产品图,它就能先帮你展开成海报、短视频封面、团购图、菜单图、优惠券和社交平台文案。\n对本地门店、小店主、小团队来说,最值钱的不是学会多少术语,而是今天就能把获客素材发出去。\n\n小生意想先跑起来,用 AI 先把宣传这一步补上。',
    createdAt: 1,
  },
  {
    id: 'm2',
    chatId: 'c1',
    sender: '苦命打工喵',
    own: false,
    kind: 'bigemoji',
    text: '9 1 7 8',
    createdAt: 2,
  },
  {
    id: 'm3',
    chatId: 'c1',
    sender: '苦命打工喵',
    own: false,
    kind: 'quote',
    text: '来自香港科技大学广州分校的一项研究显示,生命早期较少摄取糖分有助长期 🧠 脑部健康但相关证据尚未有定论。🍭',
    quoteSender: '苦命打工喵',
    quoteText: '周末为我的 🐱 猫搭建了攀爬架⚙️在评论区分享你们猫咪的照片吧',
    createdAt: 3,
  },
  {
    id: 'm4',
    chatId: 'c1',
    sender: '苦命打工喵',
    own: false,
    kind: 'text',
    text: '周末为我的 🐱 猫搭建了攀爬架⚙️\n在评论区分享你们猫咪的照片吧',
    createdAt: 4,
  },
  {
    id: 'm5',
    chatId: 'c1',
    sender: '我',
    own: true,
    kind: 'text',
    text: '好的 🐻',
    createdAt: 5,
  },
];

export const EMOJI_SETS: Record<string, string[]> = {
  全部: ['😂','🥰','😎','🥳','😍','🤩','😜','🤪','😭','😡','🥺','😴','🤯','😱','🫠','🙃','😇','🤗','🤔','🫡','😶','🤐','🤫','😪','🥱','😬','🙄','😮‍💨','😤','🤧'],
  定制: ['🐧','🐦','🐤','🦆','🦉','🐺','🐱','🐭','🐹','🐰','🦊','🐻','🐼','🐨','🐯','🦁','🐮','🐷','🐸','🐵','🐔','🦄','🐝','🦋','🐌','🐞','🦖','🐙','🦑','🦐','🐠','🐬','🐳','🦈','🐊','🦜','🦢','🦩','🐇','🦔','🐾','🌵','🎄','🌲','🌴','🍀','🍁','🍄','🌾','💐','🌷','🌹','🌻','🌼','🌸','🍎','🍊','🍋','🍉','🍇','🍓','🫐','🍑','🥭','🍍','🥝','🍅','🥑','🌶️','🌽','🥕','🧄'],
  动态: ['✨','💫','⭐','🌟','🔥','💥','🎉','🎊','🎈','🪄','🌈','⚡','❄️','🌊','💦','☄️','🎇','🎆'],
  贴纸: ['👍','👎','👏','🙏','💪','🤝','✌️','🤞','👌','🫶','❤️','🧡','💛','💚','💙','💜','🖤','🤍','💯','💢'],
  手势: ['😄','😆','😅','🤣','😂','🙂','😉','😊','😍','🤩','😘','😜','🤪','😝','🤑','🤗','🤭','🤫','🤔','🫡','😐','😑','😶','🙄','😏','😮','🤐','😪','😴','🥱'],
};

export const QUICK_PHRASES = ['666'];
