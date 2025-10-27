# .NET 9 + Consul + NGINX (reverse proxy) — Fixed
Run: `docker compose up --build`
Test:
- http://localhost:8500
- http://localhost:55080/health
- http://localhost:55080/catalog/v1/products
- POST http://localhost:55080/orders/v1/orders
