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

export interface Message {
  id: string;
  chatId: string;
  sender: string;
  own: boolean;
  kind: MessageKind;
  text: string;
  quoteSender?: string;
  quoteText?: string;
  createdAt: number;
}

export type EmojiTab = '全部' | '定制' | '动态' | '贴纸' | '手势';
export type ChatFilter = '全部' | '个人' | '群聊' | '空间';
