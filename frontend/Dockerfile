# Pantallas + nginx (HTTPS en 6010 y proxy de la API en 6011). Contexto: frontend/.
FROM node:22-alpine AS build
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# La SPA llama a la API en su propio puerto (https://<host>:6011); se fija al compilar.
ARG VITE_API_BASE_URL
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

FROM nginx:1.27-alpine
# El entrypoint de la imagen sustituye ${PUBLIC_HOST} en las plantillas al arrancar.
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /src/dist /usr/share/nginx/html
EXPOSE 6010 6011
