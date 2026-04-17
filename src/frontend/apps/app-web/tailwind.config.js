/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: 'class',
  presets: [require('@coze-arch/tailwind-config')],
  content: [
    './src/**/*.{ts,tsx}',
    '../../packages/workflow/**/*.{ts,tsx}',
    '../../packages/coze-shell-react/**/*.{ts,tsx}',
    '../../packages/library-module-react/**/*.{ts,tsx}',
    '../../packages/module-admin-react/**/*.{ts,tsx}',
    '../../packages/module-explore-react/**/*.{ts,tsx}',
    '../../packages/module-studio-react/**/*.{ts,tsx}',
  ],
  corePlugins: {
    preflight: false,
  },
  plugins: [require('@coze-arch/tailwind-config/coze')],
};
