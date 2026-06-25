import { defineConfig } from 'vite';

export default defineConfig({
  base: './',
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    sourcemap: false,
    chunkSizeWarningLimit: 4096,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (!id.includes('node_modules')) {
            return undefined;
          }

          if (id.includes('@univerjs/')) {
            const match = id.match(/@univerjs[/\\]([^/\\]+)/);
            if (match) {
              return `univer-${match[1]}`;
            }
          }

          return 'vendor';
        },
      },
    },
  },
  server: {
    port: 5174,
    open: true,
  },
});
