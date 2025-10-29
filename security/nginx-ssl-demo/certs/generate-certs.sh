#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

# Create local self-signed certs for NGINX (localhost + 127.0.0.1 + ::1)
cat > san.cnf <<'EOF'
[req]
default_bits = 2048
prompt = no
default_md = sha256
req_extensions = req_ext
distinguished_name = dn

[dn]
CN = localhost

[req_ext]
subjectAltName = @alt_names

[alt_names]
DNS.1 = localhost
IP.1  = 127.0.0.1
IP.2  = ::1
EOF

# Root CA (for demo)
openssl genrsa -out ca.key 4096
openssl req -x509 -new -nodes -key ca.key -sha256 -days 3650 \
  -subj "/CN=Local Dev Root CA" -out ca.crt

# Server key + CSR
openssl genrsa -out server.key 2048
openssl req -new -key server.key -out server.csr -config san.cnf

# Sign server cert with our CA
openssl x509 -req -in server.csr -CA ca.crt -CAkey ca.key -CAcreateserial \
  -out server.crt -days 825 -sha256 -extfile san.cnf -extensions req_ext

echo "Generated certs:"
ls -l ca.crt server.crt server.key
echo
echo "Import ca.crt into your OS trust store to avoid SSL warnings."
