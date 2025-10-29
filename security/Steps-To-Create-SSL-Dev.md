# CA private key
openssl genrsa -out ca.key 4096

# Self-signed CA certificate (ca.crt) — valid ~10 years
openssl req -x509 -new -nodes -key ca.key -sha256 -days 3650 \
  -subj "/CN=Local Dev Root CA" \
  -out ca.crt


- macOS: open Keychain Access → System → Certificates → import ca.crt, set “Always Trust”.
- Ubuntu: copy to /usr/local/share/ca-certificates/ and sudo update-ca-certificates.
- Windows: mmc → Certificates (Local Computer) → Trusted Root CAs → import.

2) Create a server cert for localhost with SANs

Create san.cnf:

Generate key + CSR:

openssl genrsa -out localhost.key 2048
openssl req -new -key localhost.key -out localhost.csr -config san.cnf

Sign CSR with your CA to get server certificate (localhost.crt):

openssl x509 -req -in localhost.csr -CA ca.crt -CAkey ca.key -CAcreateserial \
  -out localhost.crt -days 825 -sha256 -extfile san.cnf -extensions req_ext

Now you have:
	•	CA: ca.crt, ca.key
	•	Server: localhost.crt, localhost.key

3) Convert to PFX for Kestrel

openssl pkcs12 -export \
  -in localhost.crt \
  -inkey localhost.key \
  -certfile ca.crt \
  -out localhost.pfx \
  -password pass:changeit


  You have a ca.crt file (your root CA).
Import it into your OS trust store:

macOS
	1.	Open Keychain Access → System → Certificates
	2.	Drag in ca.crt
	3.	Double-click it → expand Trust → choose Always Trust

Linux

sudo cp ca.crt /usr/local/share/ca-certificates/
sudo update-ca-certificates


Windows
	1.	Run mmc
	2.	Add the Certificates (Local Computer) snap-in
	3.	Go to Trusted Root Certification Authorities → Certificates
	4.	Import ca.crt