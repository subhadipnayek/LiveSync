# syntax=docker/dockerfile:1

FROM node:24-alpine AS builder
WORKDIR /app

# Install dependencies first for better build cache usage.
COPY package*.json ./
RUN npm ci

# Build the Angular app for production.
COPY . .
RUN npm run build -- --configuration production --output-path=dist/browser

FROM nginx:1.27-alpine AS runner

# Configure Nginx for Angular client-side routing.
RUN printf 'server {\n  listen 80;\n  server_name _;\n  root /usr/share/nginx/html;\n  index index.html;\n\n  location / {\n    try_files $uri $uri/ /index.html;\n  }\n}\n' > /etc/nginx/conf.d/default.conf

COPY --from=builder /app/dist/browser/ /usr/share/nginx/html/

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
