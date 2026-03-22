FROM node:22-alpine AS build
WORKDIR /src

ARG UI_API_BASE=
ARG UI_API_KEY=dev-api-key

COPY ui/package*.json ./
RUN npm install

COPY ui/ ./

RUN printf "export const env = {\n  apiBase: '%s',\n  apiKey: '%s'\n};\n" "$UI_API_BASE" "$UI_API_KEY" > src/app/env.ts
RUN npm run build

FROM nginx:1.27-alpine AS final
WORKDIR /usr/share/nginx/html

COPY docker/central/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/ui/browser/ ./

EXPOSE 80
