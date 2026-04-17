/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: 'class',
  presets: [require('@coze-arch/tailwind-config')],
  content: [
    './src/**/*.{ts,tsx}',
    '../../packages/workflow/*/src/**/*.{ts,tsx}',
    '../../packages/coze-shell-react/src/**/*.{ts,tsx}',
    '../../packages/library-module-react/src/**/*.{ts,tsx}',
    '../../packages/module-admin-react/src/**/*.{ts,tsx}',
    '../../packages/module-explore-react/src/**/*.{ts,tsx}',
    '../../packages/module-studio-react/src/**/*.{ts,tsx}',
    '../../packages/app-shell-shared/src/**/*.{ts,tsx}',
  ],
  corePlugins: {
    preflight: false,
  },
  plugins: [require('@coze-arch/tailwind-config/coze')],
};
