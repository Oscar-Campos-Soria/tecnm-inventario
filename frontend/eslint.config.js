import js from '@eslint/js'
import globals from 'globals'
import pluginVue from 'eslint-plugin-vue'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,vue}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      pluginVue.configs['flat/recommended'],
    ],
    languageOptions: {
      globals: globals.browser,
      // <script setup lang="ts"> dentro de .vue lo parsea typescript-eslint.
      parserOptions: { parser: tseslint.parser },
    },
    rules: {
      // Tailwind deja atributos class largos: el formato lo decide quien escribe.
      'vue/max-attributes-per-line': 'off',
      'vue/singleline-html-element-content-newline': 'off',
      'vue/html-self-closing': 'off',
    },
  },
])
