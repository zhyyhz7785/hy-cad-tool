# Rebuild vendor/viewer.bundle.js after editing viewer-entry.js
# Requires Node.js + npm once.
npm install
npx esbuild viewer-entry.js --bundle --outfile=../vendor/viewer.bundle.js --format=iife --platform=browser --target=es2020
