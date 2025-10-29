# NGINX `location` Matching — Guide & Examples

This guide explains **how NGINX chooses a `location` block** for an incoming request, the different **match types** (`=`, plain prefix, `^~`, `~`, `~*`), the **selection order**, and **practical examples** you can copy into your configs.

---

## Quick Summary

- `location = /path` — **Exact** match. Wins if the URI matches exactly.
- `location ^~ /path/` — **Prefix** match that **beats any regex** (`~`, `~*`). Great for API routes.
- `location /path/` — **Plain prefix** match. Loses to any matching regex.
- `location ~ pattern` — **Regex** (case-sensitive).
- `location ~* pattern` — **Regex** (case-insensitive).

**Priority (high → low):**
1. Exact (`=`)
2. Prefix with `^~` (beats regex)
3. Regex (`~` or `~*`) — first regex that matches (topmost wins among regexes)
4. Longest plain prefix
5. Fallback `location /`

---

## How NGINX Picks a `location` (Decision Flow)

```
Request URI --> 1) Exact match?   location = /path     -> if yes, use and STOP
                 |
                 v
               2) Longest prefix? location /path       -> record the LONGEST prefix
                 |
                 v
               3) Any regex match? location ~ /regex
                                   location ~* /regex  -> if a regex matches, pick the FIRST regex that matches
                 |
                 v
               4) If a chosen prefix had ^~, it BEATS regex. Otherwise regex beats plain prefix.
                 |
                 v
               5) If no regex wins, use the LONGEST prefix.
```

---

## Match Types (with Minimal Examples)

### 1) Exact match (`=`)
```nginx
location = /healthz {
  return 200 "ok\n";
}
```
- Matches **only** `/healthz` (not `/healthz/` or `/healthz/v2`).

### 2) Plain prefix match
```nginx
location /api/ {
  proxy_pass http://api_service;
}
```
- Matches anything that **starts with** `/api/`.
- **Can be overridden by regex** if a regex matches too.

### 3) Prefix match that beats regex (`^~`)
```nginx
location ^~ /api/order/ {
  proxy_pass http://order_service;
}
```
- Matches `/api/order/...` and **prevents** regex locations from taking over.
- Use this for predictable API routing.

### 4) Regex (case-sensitive) (`~`) and case-insensitive (`~*`)
```nginx
location ~ ^/assets/.+\.(?:png|jpg|svg)$ {
  root /var/www;
}

location ~* \.(css|js)$ {
  root /var/www;
}
```
- `~` is case-sensitive; `~*` ignores case.
- Among multiple regexes, **the first one that matches in the file wins**.

---

## Full Example

```nginx
server {
  listen 8080;

  # 1) Exact
  location = /healthz { return 200 "ok\n"; }

  # 2) High-priority API routes (prefix that beats regex)
  location ^~ /api/inventory/ { proxy_pass http://inventory_service; }
  location ^~ /api/order/     { proxy_pass http://order_service; }

  # 3) Regex for static assets (case-insensitive)
  location ~* \.(?:css|js|png|jpg|svg|ico)$ {
    root /var/www/static;
    expires 7d;
  }

  # 4) Fallback
  location / { try_files $uri /index.html; }
}
```

---

## Which One Wins? (Concrete Requests)

Assume the config above. This table explains the winner for each request and **why**.

| Request URI                     | Winner `location`                      | Why |
|---------------------------------|----------------------------------------|-----|
| `/healthz`                      | `= /healthz`                           | Exact match has highest priority |
| `/api/inventory/hello`         | `^~ /api/inventory/`                   | `^~` prefix beats any regex |
| `/api/inventory/icon.svg`      | `^~ /api/inventory/`                   | `^~` blocks regex takeover |
| `/api/order/123`               | `^~ /api/order/`                       | `^~` prefix |
| `/assets/logo.svg`             | `~* \.(css|js|png|jpg|svg|ico)$`      | No `^~` prefix applies; regex beats plain `/` |
| `/`                            | `/`                                    | Fallback prefix |
| `/unknown`                     | `/`                                    | Fallback prefix |

---

## Path Rewriting with `proxy_pass` (Gotchas)

The **presence or absence of a trailing slash** after `proxy_pass` changes how NGINX builds the upstream request URI.

```nginx
# 1) No trailing slash on proxy_pass: keep full original URI
location ^~ /api/inventory/ {
  proxy_pass http://inventory_service;
}
# GET /api/inventory/hello  -->  http://inventory_service/api/inventory/hello

# 2) Trailing slash on proxy_pass: strip matching prefix and append remainder
location ^~ /api/inventory/ {
  proxy_pass http://inventory_service/;
}
# GET /api/inventory/hello  -->  http://inventory_service/hello
```

**Tip:** For microservice routing where upstream expects the full path, prefer **no trailing slash** on `proxy_pass`.

---

## Best Practices

- Use `=` for **one-off** exactly matched routes (e.g., `/healthz`, `/metrics` if local only).
- Use `^~` for **stable API prefixes** (`/api/order/`, `/api/inventory/`) to avoid surprises from regex.
- Put **regex locations after** your `^~` API blocks to avoid accidental overrides.
- Keep a **fallback** `location /` at the end for catch-all behavior.
- Be mindful of **path rewriting** behavior with `proxy_pass` (trailing slash changes the upstream URL).
- If you need to truly **delete a request header**, OSS NGINX can’t without `headers-more` module; don’t send an empty header value (some upstreams return **400 Bad Request**).

---

## Quick Troubleshooting

- `curl -v http://host/path` to see response headers and chosen path.
- From container: `nginx -T` prints the **effective** loaded config.
- `nginx -t` validates syntax before reload.
- Add `add_header X-Debug-Loc "inventory";` in a location while debugging to confirm which block matched.

---

## Cheatsheet

```nginx
# Priority: 1) =exact  2) ^~prefix  3) ~regex/~*regex  4) longest plain prefix  5) /

location = /healthz               { return 200 "ok\n"; }
location ^~ /api/order/           { proxy_pass http://order_service; }
location ^~ /api/inventory/       { proxy_pass http://inventory_service; }
location ~* \.(css|js|png|jpg)$  { root /var/www/static; }
location /                        { try_files $uri /index.html; }
```