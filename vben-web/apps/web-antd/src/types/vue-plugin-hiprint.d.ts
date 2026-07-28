declare module 'vue-plugin-hiprint' {
  export const defaultElementTypeProvider: new () => unknown;
  export function disAutoConnect(): void;
  export const hiprint: {
    init(options?: Record<string, unknown>): void;
    PrintElementTypeManager: {
      build(selector: string, providerName: string): void;
    };
    PrintTemplate: new (options: Record<string, unknown>) => any;
  };
}
