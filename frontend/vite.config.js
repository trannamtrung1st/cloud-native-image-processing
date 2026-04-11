import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  // Docker / CI: compose passes --build-arg → Dockerfile ENV; process.env must win over .env files.
  const viteApiBaseUrl = process.env.VITE_API_BASE_URL ?? env.VITE_API_BASE_URL ?? ''
  let base = (env.VITE_BASE_URL || process.env.VITE_BASE_URL || '/').trim()
  if (base !== '/' && !base.endsWith('/')) {
    base = `${base}/`
  }

  return {
    base: base === '' ? '/' : base,
    define: {
      'import.meta.env.VITE_API_BASE_URL': JSON.stringify(viteApiBaseUrl),
    },
    plugins: [react(), tailwindcss()],
    server: {
      proxy: {
        "/api": {
          target: "http://localhost:5000",
          changeOrigin: true,
        },
      },
    },
  }
})
