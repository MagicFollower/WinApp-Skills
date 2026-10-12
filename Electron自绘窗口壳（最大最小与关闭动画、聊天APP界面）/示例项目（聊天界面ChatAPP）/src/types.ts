export type ChatType = 'personal' | 'group' | 'space';
export type ChatThemeId = 'warm' | 'mint';

export interface Chat {
  id: string;
  type: ChatType;
  name: string;
  members: number;
  /** 群号/QQ 号:按字符串存,号码有前导位且不参与算术。 */
  qq: string;
  avatar: string;
  avatarBg: string;
  themeId: ChatThemeId;
}

export type MessageKind = 'text' | 'image' | 'bigemoji' | 'quote';

/** 一段草稿在气泡里吃哪个字号档:由"从哪一格进来的"决定。
    text=打字与粘贴,sm=表情 tab(picker)的小表情,lg=大表情 tab。
    不靠文本形状猜:猜会把 666 这类纯数字判成大表情。 */
export type GlyphSize = 'text' | 'sm' | 'lg';

export interface MessagePart {
  text: string;
  size: GlyphSize;
}

export interface Message {
  id: string;
  chatId: string;
  sender: string;
  own: boolean;
  kind: MessageKind;
  text: string;
  /** 只有从输入框发出的消息带分段;种子消息与图片/引用不带,按 text 整段渲染。 */
  parts?: MessagePart[];
  quoteSender?: string;
  quoteText?: string;
  createdAt: number;
}

export type EmojiTab = '表情' | '大表情';
export type ChatFilter = '全部' | '个人' | '群聊' | '空间';
