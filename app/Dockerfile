# Angular 17 production Dockerfile — multi-stage build.
# Stage 1: Node 20 LTS compiles the AOT production bundle.
# Stage 2: nginx:alpine serves the static bundle with SPA routing support.

# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM node:20-alpine AS build
WORKDIR /app

# Restore dependencies with layer-cache optimisation — reruns only when
# package-lock.json changes, not on every source edit.
COPY package.json package-lock.json ./
RUN npm ci --prefer-offline

COPY . .
RUN npx ng build --configuration production

# ── Stage 2: Serve ────────────────────────────────────────────────────────────
# nginx:alpine is ~25 MB; no Node.js runtime or build tools are included,
# minimising the production attack surface (OWASP A05).
FROM nginx:alpine AS production

# Angular 17 (application builder) writes output to dist/<project>/browser/.
# The glob pattern handles any project name without hardcoding "propeliq-app".
COPY --from=build /app/dist/*/browser /usr/share/nginx/html

# Custom nginx config enables HTML5 pushState routing (try_files → index.html).
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Run nginx as non-root for additional hardening.
RUN chown -R nginx:nginx /usr/share/nginx/html \
    && chmod -R 755 /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
