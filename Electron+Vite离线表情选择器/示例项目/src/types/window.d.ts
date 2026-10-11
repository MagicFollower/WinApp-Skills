export {};

declare global {
  interface Window {
    api: {
      ping(): Promise<string>;
      env(): Promise<{ themeSource: string; shouldUseDarkColors: boolean }>;
    };
  }
}
