/// <reference types="vite/client" />

interface WindowApi {
  ping: () => Promise<string>;
  minimize: () => Promise<void>;
  maximize: () => Promise<void>;
  close: () => Promise<void>;
  dragStart: () => void;
  dragMove: () => void;
  dragEnd: () => void;
  onRestore: (cb: () => void) => void;
}

interface Window {
  api?: WindowApi;
}
