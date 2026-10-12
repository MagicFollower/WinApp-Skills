interface IconProps {
  size?: number;
  className?: string;
}

const base = (size: number) => ({
  width: size,
  height: size,
  viewBox: '0 0 24 24',
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 1.5,
  strokeLinecap: 'round' as const,
  strokeLinejoin: 'round' as const,
});

export const IconMenu = ({ size = 20 }: IconProps) => (
  <svg {...base(size)}>
    <path d="M4 7h16M4 12h16M4 17h16" />
  </svg>
);

export const IconSearch = ({ size = 16 }: IconProps) => (
  <svg {...base(size)}>
    <circle cx="11" cy="11" r="7" />
    <path d="m20 20-3.5-3.5" />
  </svg>
);

export const IconBell = ({ size = 20 }: IconProps) => (
  <svg {...base(size)}>
    <path d="M6 9a6 6 0 1 1 12 0c0 5 2 6 2 6H4s2-1 2-6" />
    <path d="M10 20a2 2 0 0 0 4 0" />
  </svg>
);

export const IconDots = ({ size = 20 }: IconProps) => (
  <svg {...base(size)} fill="currentColor" stroke="none">
    <circle cx="12" cy="5" r="1.8" />
    <circle cx="12" cy="12" r="1.8" />
    <circle cx="12" cy="19" r="1.8" />
  </svg>
);

export const IconFile = ({ size = 20 }: IconProps) => (
  <svg {...base(size)}>
    <path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z" />
    <path d="M14 3v5h5" />
  </svg>
);

export const IconImage = ({ size = 20 }: IconProps) => (
  <svg {...base(size)}>
    <rect x="3" y="4" width="18" height="16" rx="3" />
    <circle cx="9" cy="10" r="1.6" />
    <path d="m5 18 5-5 3 3 3-3 3 3" />
  </svg>
);

export const IconSmile = ({ size = 20 }: IconProps) => (
  <svg {...base(size)}>
    <circle cx="12" cy="12" r="9" />
    <path d="M8.5 14a4.5 4.5 0 0 0 7 0" />
    <path d="M9 9.5h.01M15 9.5h.01" strokeWidth="2.2" />
  </svg>
);

export const IconSend = ({ size = 22 }: IconProps) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="currentColor">
    <path d="M3.4 20.4 21.8 12 3.4 3.6l3 7.2 8.4 1.2-8.4 1.2z" />
  </svg>
);

export const IconDown = ({ size = 14 }: IconProps) => (
  <svg {...base(size)} strokeWidth="2.2">
    <path d="M12 4v12m0 0 5-5m-5 5-5-5" />
  </svg>
);

export const IconClose = ({ size = 14 }: IconProps) => (
  <svg {...base(size)} strokeWidth="2">
    <path d="M6 6l12 12M18 6 6 18" />
  </svg>
);

export const IconExpand = ({ size = 14 }: IconProps) => (
  <svg {...base(size)} strokeWidth="2">
    <path d="M9 4H4v5M15 4h5v5M9 20H4v-5M15 20h5v-5" />
  </svg>
);

export const IconChevron = ({ size = 14 }: IconProps) => (
  <svg {...base(size)} strokeWidth="2">
    <path d="m6 9 6 6 6-6" />
  </svg>
);

export const IconMic = ({ size = 16 }: IconProps) => (
  <svg {...base(size)}>
    <rect x="9" y="3" width="6" height="11" rx="3" />
    <path d="M5 11a7 7 0 0 0 14 0M12 18v3" />
  </svg>
);

export const IconPaw = ({ size = 14 }: IconProps) => (
  <svg {...base(size)}>
    <circle cx="8" cy="9" r="1.6" />
    <circle cx="16" cy="9" r="1.6" />
    <circle cx="5" cy="13" r="1.4" />
    <circle cx="19" cy="13" r="1.4" />
    <path d="M12 12c-2.6 0-4.5 2.2-4.5 4.2 0 1.4 1 2.3 2.2 2.3 1 0 1.5-.5 2.3-.5s1.3.5 2.3.5c1.2 0 2.2-.9 2.2-2.3C16.5 14.2 14.6 12 12 12Z" />
  </svg>
);

export const IconPerson = ({ size = 14 }: IconProps) => (
  <svg {...base(size)}>
    <circle cx="12" cy="8" r="3.5" />
    <path d="M5 20c1.5-3.5 4-5 7-5s5.5 1.5 7 5" />
  </svg>
);

export const IconGroup = ({ size = 14 }: IconProps) => (
  <svg {...base(size)}>
    <circle cx="9" cy="9" r="3" />
    <path d="M3.5 19c1-3 3-4.5 5.5-4.5s4.5 1.5 5.5 4.5" />
    <path d="M15.5 6.5a3 3 0 0 1 0 5.5M17.5 14.7c1.6.7 2.6 2 3.1 4.3" />
  </svg>
);

export const IconSpace = ({ size = 14 }: IconProps) => (
  <svg {...base(size)}>
    <circle cx="12" cy="12" r="4" />
    <ellipse cx="12" cy="12" rx="9.5" ry="3.8" transform="rotate(-20 12 12)" />
  </svg>
);

export const IconMin = ({ size = 14 }: IconProps) => (
  <svg {...base(size)} strokeWidth="2">
    <path d="M5 12h14" />
  </svg>
);

export const IconMax = ({ size = 14 }: IconProps) => (
  <svg {...base(size)} strokeWidth="2">
    <rect x="5" y="5" width="14" height="14" rx="2" />
  </svg>
);
