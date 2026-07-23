import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'
export default defineConfig({
  base: '',              // 空字符串 = 相对路径（WebView2 必需）
  plugins: [
    react(),
    tailwindcss(),
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  build: {
    chunkSizeWarningLimit: 1000,
    outDir: 'D:/AVEVA/Everything3D2.10/wwwroot',
    emptyOutDir: true,
    sourcemap: false,
    rollupOptions: {
      output: {
        manualChunks: {
          'vendor-react': ['react', 'react-dom'],
          'vendor-zustand': ['zustand'],
          'vendor-icons': ['lucide-react'],
          'vendor-markdown': ['react-markdown', 'rehype-katex', 'rehype-highlight', 'remark-math', 'katex'],
        },
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/__tests__/**/*.{test,spec}.{ts,tsx}'],
    server: {
      deps: {
        inline: ['react', 'react-dom'],
      },
    },
  },
})
