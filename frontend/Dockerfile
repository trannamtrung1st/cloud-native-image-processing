FROM node:22-alpine AS build
WORKDIR /app

COPY package.json yarn.lock ./
RUN yarn install --frozen-lockfile

COPY . .
# Production behind ingress: leave VITE_API_BASE_URL empty so the SPA calls /api on the same origin.
ARG VITE_API_BASE_URL=
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
# Set to public origin with trailing slash when serving from a known URL (e.g. http://1.2.2.2/); default / for relative assets.
ARG VITE_BASE_URL=/
ENV VITE_BASE_URL=${VITE_BASE_URL}
RUN yarn build

FROM nginx:1.27-alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
