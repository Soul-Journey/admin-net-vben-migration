FROM node:22-slim AS builder

# --max-old-space-size
ENV PNPM_HOME="/pnpm"
ENV PATH="$PNPM_HOME:$PATH"
ENV NODE_OPTIONS=--max-old-space-size=8192
ENV TZ=Asia/Shanghai
ENV CI=true

RUN npm i -g corepack

WORKDIR /app

# copy package.json and pnpm-lock.yaml to workspace
COPY . /app

# 安装依赖
RUN --mount=type=cache,id=pnpm,target=/pnpm/store pnpm install --frozen-lockfile
RUN pnpm run build --filter=\!./docs

RUN echo "Builder Success 🎉"

FROM nginx:stable-alpine AS production

# Admin.NET 后端地址可在 docker run / compose 中覆盖。
ENV ADMINNET_BACKEND=http://host.docker.internal:5005

# 配置 nginx
RUN echo "types { application/javascript js mjs; }" > /etc/nginx/conf.d/mjs.conf \
    && rm -rf /etc/nginx/conf.d/default.conf

# 复制构建产物
COPY --from=builder /app/apps/web-antd/dist /usr/share/nginx/html

# 官方 nginx 镜像启动时会把 ADMINNET_BACKEND 注入模板。
COPY --from=builder /app/scripts/deploy/adminnet.conf.template /etc/nginx/templates/default.conf.template

EXPOSE 8080

# 启动 nginx
CMD ["nginx", "-g", "daemon off;"]
