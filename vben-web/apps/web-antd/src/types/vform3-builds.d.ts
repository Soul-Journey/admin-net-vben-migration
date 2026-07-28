declare module 'vform3-builds' {
  import type { App, Component } from 'vue';

  export interface VFormDesignerExpose {
    clearDesigner: () => void;
    exportJson: () => void;
    getFormJson: () => Record<string, unknown>;
    importJson: () => void;
    previewForm: () => void;
    setFormJson: (formJson: Record<string, unknown> | string) => void;
  }

  interface VForm3Plugin {
    install: (app: App) => void;
    VFormDesigner: Component;
    VFormRender: Component;
  }

  const VForm3: VForm3Plugin;
  export default VForm3;
}
