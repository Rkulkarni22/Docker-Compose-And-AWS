# LOS App: Docker Compose Deployment Guide for AWS EC2

A 3-tier app (React/Vite frontend, .NET backend, PostgreSQL) running with Docker Compose on a single EC2 instance.

## 1. How it works

```
Browser ──HTTP :80──> nginx (frontend container)
                        ├── /      -> React static files
                        └── /api/  -> proxy -> backend:8080 (.NET)
                                                   └── postgres:5432
```

- The browser talks **only to port 80**. nginx forwards `/api/...` to the backend over the internal Docker network.
- Postgres and the backend are **not exposed to the internet**.
- No CORS setup is needed, because the browser sees a single origin.
- `localhost:8080` must never be used in the deployed frontend. In the browser, `localhost` means the visitor's own computer.

## 2. Repository layout

```
repo/
├── docker-compose.yml
├── .env                          <- compose variables (DB password), do NOT commit
├── init.sql
├── README.md
├── LOS_Backend_Service/
│   └── LOS_Backend_Service/
│       └── Dockerfile            <- already exists
└── LOS_Frontend/
    ├── .dockerignore
    ├── .env                      <- VITE_API_BASE_URL=/api
    ├── Dockerfile
    ├── nginx.conf
    └── src/api/client.ts
```

## 3. Files to create or replace

### 3.1 `LOS_Frontend/nginx.conf`

```nginx
server {
    listen 80;

    location / {
        root /usr/share/nginx/html;
        index index.html;
        try_files $uri $uri/ /index.html;
    }

    location /api/ {
        proxy_pass http://backend:8080/api/;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Notes:

- `backend` must match the **service name** in `docker-compose.yml`. To list your service names, run `docker compose config --services`.
- `8080` is the port the app listens on **inside** the container.
- If your controllers do **not** start with `/api` (for example `/Home`), use `proxy_pass http://backend:8080/;` instead. The trailing `/` makes nginx strip `/api` before forwarding.

### 3.2 `LOS_Frontend/Dockerfile`

```dockerfile
# ---- Build stage ----
FROM node:20-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .

# Vite reads VITE_* variables at BUILD time
ARG VITE_API_BASE_URL=/api
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

# ---- Runtime stage ----
FROM nginx:alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
```

### 3.3 `LOS_Frontend/.dockerignore`

```
node_modules
dist
.git
```

Do not list `.env` here unless you rely only on the `ARG` above.

### 3.4 `LOS_Frontend/.env`

```
VITE_API_BASE_URL=/api
```

### 3.5 `LOS_Frontend/src/api/client.ts`

Your existing file is already correct. It reads the value from the environment:

```ts
import axios from "axios";

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  headers: { "Content-Type": "application/json" },
});

export default apiClient;
```

**Avoid a double `/api`:** with `baseURL = /api`, calls in `userService.ts` should be `apiClient.get("/Home")`, not `apiClient.get("/api/Home")`. Otherwise the request becomes `/api/api/Home`.

### 3.6 Root `.env` (for Docker Compose)

```
POSTGRES_PASSWORD=choose-a-strong-password
```

Add it to `.gitignore`. Use a strong password instead of `admin`.

### 3.7 `docker-compose.yml`

```yaml
services:
  postgres:
    image: postgres:16
    restart: unless-stopped
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: LOS_DB
    volumes:
      - pgdata:/var/lib/postgresql/data
      - ./init.sql:/docker-entrypoint-initdb.d/init.sql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres -d LOS_DB"]
      interval: 5s
      timeout: 5s
      retries: 10
    # No ports: the DB is only reachable from other containers

  backend:
    build:
      context: .
      dockerfile: LOS_Backend_Service/LOS_Backend_Service/Dockerfile
    restart: unless-stopped
    environment:
      ASPNETCORE_URLS: "http://+:8080"
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=LOS_DB;Username=postgres;Password=${POSTGRES_PASSWORD}"
    depends_on:
      postgres:
        condition: service_healthy
    # No ports: only nginx talks to the backend

  frontend:
    build:
      context: ./LOS_Frontend
      args:
        VITE_API_BASE_URL: /api
    restart: unless-stopped
    depends_on:
      - backend
    ports:
      - "80:80"

volumes:
  pgdata:
```

**Temporary debugging:** to test the backend directly, add this under `backend`, then remove it again afterwards:

```yaml
    ports:
      - "8080:8080"
```

### 3.8 Backend checks (.NET `Program.cs`)

- The app must listen on `0.0.0.0:8080`. `ASPNETCORE_URLS=http://+:8080` in the compose file does this.
- Remove or comment out `app.UseHttpsRedirection();` for the container. There is no HTTPS inside it, and the redirect can break requests.
- If you want to call the backend directly from a different origin (not through nginx), add CORS. With the proxy setup this is not required.

## 4. AWS setup (EC2)

### 4.1 Launch the instance

1. EC2 console, then **Launch instance**.
2. AMI: **Ubuntu Server 22.04 or 24.04 LTS**.
3. Type: `t3.small` or larger (a `t2.micro` often runs out of memory building .NET and Node images).
4. Storage: 20 GB or more.
5. Create or select a key pair and download the `.pem` file.

### 4.2 Security group (inbound rules)

| Type | Port | Source | Purpose |
|---|---|---|---|
| SSH | 22 | **My IP** | Admin access |
| HTTP | 80 | 0.0.0.0/0 | The website |
| HTTPS | 443 | 0.0.0.0/0 | Only if you add TLS later |

Do **not** open 5432 or 8080 to the internet.

### 4.3 Attach an Elastic IP (recommended)

The public IP changes every time the instance stops and starts. Allocate an **Elastic IP** and associate it with the instance.

### 4.4 Connect

```bash
ssh -i your-key.pem ubuntu@<PUBLIC-IP>
```

Use the **Public IPv4 address**, not the private `172.31.x.x` address.

### 4.5 Install Docker and Compose

```bash
sudo apt update
sudo apt install -y ca-certificates curl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}") stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

sudo usermod -aG docker $USER
newgrp docker
docker compose version
```

### 4.6 Deploy

```bash
git clone <your-repo-url> app
cd app

# create the root .env with the DB password
echo "POSTGRES_PASSWORD=choose-a-strong-password" > .env

docker compose up -d --build
docker compose ps
```

All three services should show `Up` (postgres should show `healthy`).

## 5. Verify, from the inside out

Run on the EC2 instance:

```bash
# 1. Containers
docker compose ps

# 2. Backend reachable from inside the Docker network
docker compose exec frontend wget -qO- http://backend:8080/api/Home

# 3. Through nginx on the server
curl -i http://localhost/api/Home

# 4. DB has the data
docker compose exec postgres psql -U postgres -d LOS_DB -c "select count(*) from user_master;"
```

Then from your own computer:

```bash
curl -i http://<PUBLIC-IP>/api/Home
```

Open `http://<PUBLIC-IP>` in the browser (use `http://`, not `https://`). In DevTools, Network tab, the API calls should go to `http://<PUBLIC-IP>/api/...`.

## 6. Applying changes later

| What changed | Command |
|---|---|
| `docker-compose.yml` values | `docker compose up -d` |
| Code, Dockerfile, `nginx.conf`, or Vite `.env` | `docker compose up -d --build` |
| Only the frontend | `docker compose up -d --build frontend` |
| Stale build cache | `docker compose build --no-cache frontend` then `docker compose up -d` |

After a frontend rebuild, hard refresh the browser (Ctrl+Shift+R) to avoid the cached JS bundle.

## 7. Stopping and cleaning up

| Command | Effect |
|---|---|
| `docker compose stop` | Stop containers, keep them |
| `docker compose down` | Remove containers and network, **keep DB data** |
| `docker compose down -v` | Also **delete the DB volume** (data loss, but re-runs `init.sql`) |

`init.sql` only runs when the `pgdata` volume is empty.

## 8. Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `ERR_CONNECTION_TIMED_OUT` in browser | Frontend calls a public/private IP on a blocked port (for example `:8080`) | Rebuild with `VITE_API_BASE_URL=/api`; do not call `:8080` from the browser |
| `ERR_CONNECTION_REFUSED` to `localhost:8080` | Old `.env` baked into the build | Rebuild with `--no-cache` and hard refresh |
| Site does not load at all | Port 80 not open, or wrong IP | Check security group; use the public IP |
| 502 Bad Gateway on `/api` | nginx can't reach the backend | `docker compose logs backend`; check the service name in `proxy_pass` |
| 404 on `/api/...` | Path mapping mismatch | Use `proxy_pass http://backend:8080/;` if routes have no `/api` prefix |
| Request goes to `/api/api/...` | `/api` in both baseURL and call path | Remove `/api` from the call paths |
| Frontend container exits: `host not found in upstream "backend"` | Backend not running, or wrong service name | Fix the backend; match the name from `docker compose config --services` |
| Backend crashes at startup (Npgsql error) | DB not ready, or wrong connection string | Use the postgres healthcheck; check the password matches |
| App loads but the DB is empty | `init.sql` didn't run (volume already existed) | `docker compose down -v`, then `up -d --build` (**deletes data**) |
| `permission denied ... docker.sock` | User not in docker group | `sudo usermod -aG docker $USER`, then re-login |
| Unable to locate package `docker-compose-plugin` | Docker apt repo missing | Follow section 4.5 |

Useful debugging commands:

```bash
docker compose ps -a
docker compose logs --tail=50 backend
docker compose logs --tail=50 frontend
docker compose logs --tail=50 postgres
docker compose exec frontend sh -c "grep -rhoE 'https?://[A-Za-z0-9.:-]+' /usr/share/nginx/html/assets | sort -u"
```

The last command shows which URLs are baked into the deployed JavaScript. You should **not** see `localhost:8080` or an IP with `:8080`.

## 9. Optional next steps

- **HTTPS:** point a domain at the Elastic IP, then add TLS with Caddy or nginx plus Certbot. Open port 443.
- **Managed database:** move Postgres to **Amazon RDS** and point the connection string at it.
- **Secrets:** keep passwords out of Git. Use the root `.env` (git-ignored) or AWS Secrets Manager.
- **Backups:** `docker compose exec postgres pg_dump -U postgres LOS_DB > backup.sql`
