# .NET 9 + Consul + NGINX (reverse proxy) — Fixed
Run: `docker compose up --build`
Test:
- http://localhost:8500
- http://localhost:55080/health
- http://localhost:55080/catalog/v1/products
- POST http://localhost:55080/orders/v1/orders


	1.	Exact match (=)
	2.	Longest prefix match (^~ or no modifier)
	3.	First matching regex (~ or ~*)
	4.	Default prefix (/)