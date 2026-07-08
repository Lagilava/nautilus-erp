# Build the SPA, then serve the static bundle with nginx (which also proxies the API).
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . ./
RUN npm run build

FROM nginx:1.27-alpine AS runtime

# The nginx image runs envsubst over /etc/nginx/templates/*.template at start-up and writes the
# result into /etc/nginx/conf.d. That is how ${API_ORIGIN} becomes a real upstream without baking
# the API's address into the image.
COPY nginx.default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html

# host:port of the API. Sensible default for docker-compose; Render overrides it with the API's
# internal address. The scheme lives in the template, because Render exposes host:port only.
ENV API_UPSTREAM=api:8080

# Substitute only this, so nginx's own $host / $uri / $scheme survive untouched.
ENV NGINX_ENVSUBST_FILTER=API_UPSTREAM

EXPOSE 80
