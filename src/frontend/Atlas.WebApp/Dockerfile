FROM node:22-alpine AS build
WORKDIR /app

COPY src/frontend/Atlas.WebApp/package*.json ./
RUN npm ci

COPY src/frontend/Atlas.WebApp/ ./
RUN npm run build

FROM nginx:1.27-alpine AS runtime
WORKDIR /usr/share/nginx/html

COPY src/frontend/Atlas.WebApp/nginx/default.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/ /usr/share/nginx/html/

EXPOSE 80
HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=12 CMD wget -q -O /dev/null http://127.0.0.1/ || exit 1
