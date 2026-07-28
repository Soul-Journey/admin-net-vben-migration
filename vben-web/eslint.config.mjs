import { defineConfig } from '@vben/eslint-config';

export default defineConfig([
  {
    files: ['**/*.vue'],
    rules: {
      // Oxfmt is the single source of truth for Vue template line wrapping.
      'vue/html-closing-bracket-newline': 'off',
      'vue/multiline-html-element-content-newline': 'off',
    },
  },
]);
