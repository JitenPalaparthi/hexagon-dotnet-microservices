# 🔐 OAuth 2.0 and OpenID Connect (OIDC) — Concepts, Flows, and Use Cases

## 1. Introduction

Modern applications often need to access user data securely across multiple systems without sharing passwords.  
Two key standards make this possible:
- **OAuth 2.0** — for **authorization** (who can access what)
- **OpenID Connect (OIDC)** — for **authentication** (who you are)

---

## 2. OAuth 2.0 Overview

**OAuth 2.0** is an *authorization framework* that allows third-party applications to obtain limited access to a web service (e.g., Google, GitHub, Facebook) on behalf of a user — without exposing user credentials.

> Think of OAuth 2.0 as a secure *“valet key”* — you give access to what’s needed, not your full account.

### 🔑 Core Roles

| Role | Description |
|------|--------------|
| **Resource Owner** | The user who owns the data. |
| **Client (Application)** | App requesting access (e.g., Spotify using your Google account). |
| **Authorization Server** | Issues tokens after authenticating and authorizing the user. |
| **Resource Server** | Hosts protected data (e.g., Google API). |

---

## 3. OAuth 2.0 Flow Overview

A typical OAuth 2.0 flow looks like this:

```
+--------+                                           +---------------+
|        |--(A) Authorization Request--------------->| Authorization |
|        |                                           |     Server    |
|        |<-(B) Authorization Grant------------------|               |
| Client |                                           +---------------+
|        |
|        |--(C) Authorization Grant + Client Secret->|
|        |                                           |
|        |<-(D) Access Token-------------------------|
|        |
|        |--(E) Access Token------------------------>| Resource      |
|        |                                           |    Server     |
|        |<-(F) Protected Resource------------------|               |
+--------+                                           +---------------+
```

---

## 4. OAuth 2.0 Grant Types (Flows)

### 4.1 Authorization Code Flow (Most Common)

Used by web and mobile apps that can securely store secrets.

#### Steps:
1. User logs in via the Authorization Server.
2. Authorization Server redirects with a temporary **authorization code**.
3. Client exchanges the code for an **access token** (and optionally a **refresh token**).
4. Access token is used to call APIs.

**Example:**
```
https://accounts.google.com/o/oauth2/v2/auth?
  client_id=abc123
  &redirect_uri=https://myapp.com/callback
  &response_type=code
  &scope=email profile
```

Then exchange the code:
```bash
POST https://oauth2.googleapis.com/token
Content-Type: application/x-www-form-urlencoded

code=abcd1234&client_id=abc123&client_secret=xyz789&redirect_uri=https://myapp.com/callback&grant_type=authorization_code
```

---

### 4.2 Client Credentials Flow

Used for **server-to-server** communication where no user is involved.

**Example:** Backend service fetching data from another API.

```
POST /token
grant_type=client_credentials
client_id=serviceA
client_secret=secret
```

Response:
```json
{ "access_token": "abc.def.ghi", "expires_in": 3600 }
```

---

### 4.3 Resource Owner Password Credentials Flow (Deprecated)

Used when the app collects username/password directly — **NOT recommended** for modern systems.

---

### 4.4 Implicit Flow (Legacy for SPAs)

Used for single-page apps (SPAs) that can’t store secrets securely.  
Now replaced by **Authorization Code Flow with PKCE**.

---

### 4.5 Refresh Token Flow

Used to renew expired access tokens without re-login.

```
POST /token
grant_type=refresh_token
refresh_token=abc123
client_id=myapp
```

---

## 5. Tokens in OAuth 2.0

| Token Type | Description | Example |
|-------------|-------------|----------|
| **Access Token** | Used to access protected APIs | `eyJhbGciOi...` |
| **Refresh Token** | Used to get new access tokens | `r1.slkdjoq123` |
| **ID Token** | (from OIDC) contains user identity info | `{ "sub": "123", "email": "user@domain.com" }` |

---

## 6. OpenID Connect (OIDC)

**OpenID Connect** is an *identity layer* built on top of OAuth 2.0.  
It adds **authentication** to OAuth’s authorization framework.

> OAuth = “Can this app access my data?”  
> OpenID Connect = “Who is this user?”

OIDC issues an **ID Token** (JWT) that contains user identity information.

**Example ID Token:**
```json
{
  "iss": "https://accounts.google.com",
  "sub": "1234567890",
  "email": "user@gmail.com",
  "name": "Jiten Palaparthi",
  "exp": 1693412400
}
```

---

## 7. OIDC Flow — Authorization Code Flow

```
   +--------+                                  +---------------+
   |        |--(1) /authorize?response_type=code ------------->|
   |        |                                  | Authorization |
   |        |<-(2) Authorization Code----------|    Server     |
   | Client |                                  +---------------+
   |        |--(3) POST /token (with code)---->|
   |        |<-(4) Access Token + ID Token-----|
   |        |
   |        |--(5) Access Token--------------->| Resource Server |
   |        |<-(6) Protected Data--------------|                 |
   +--------+                                  +----------------+
```

---

## 8. Use Cases

### 🧭 Use Case 1 — “Login with Google” (OpenID Connect)
1. User clicks **Login with Google**.
2. App redirects to Google’s OAuth endpoint.
3. User signs in → Google sends **authorization code**.
4. App exchanges code for tokens (access, ID, refresh).
5. App verifies ID Token to authenticate user.

Result: App knows **who** the user is (OIDC) and can access Google APIs (OAuth).

---

### 🧩 Use Case 2 — “API Access via OAuth”
1. Backend service (e.g., Order Service) requests access to another (e.g., Payment API).
2. Uses **Client Credentials Flow**.
3. Authorization Server issues Access Token.
4. Order Service calls Payment API with that token.

---

### 🛡️ Use Case 3 — Mobile App + API Gateway
- Mobile app uses **Authorization Code with PKCE** flow.
- Authorization Server issues JWT Access Token.
- API Gateway validates JWT signature before forwarding requests to microservices.

---

## 9. Comparing OAuth 2.0 vs OpenID Connect

| Feature | OAuth 2.0 | OpenID Connect |
|----------|------------|----------------|
| **Purpose** | Authorization | Authentication + Authorization |
| **Token** | Access Token | Access Token + ID Token |
| **User Identity** | Not provided | Provided via ID Token |
| **Protocol** | RFC 6749 | Extends OAuth 2.0 |
| **Example** | Access Google Drive | “Login with Google” |

---

## 10. Security Best Practices

✅ Use **Authorization Code Flow with PKCE** for SPAs and mobile apps.  
✅ Always use **HTTPS**.  
✅ Validate token **issuer**, **audience**, and **expiration**.  
✅ Never store secrets in frontend code.  
✅ Use **short-lived** access tokens and **refresh tokens**.  
✅ Prefer **OpenID Connect** for user authentication.

---

## 11. Summary

| Concept | Description |
|----------|-------------|
| **OAuth 2.0** | Delegated authorization — “Can app X access resource Y?” |
| **OIDC** | Authentication layer — “Who is this user?” |
| **Access Token** | Grants API access |
| **ID Token** | Identifies the user |
| **Refresh Token** | Extends session without login |
| **PKCE** | Protects against interception in public clients |

---

## 12. Real-World Example Summary

| Scenario | Protocol | Flow | Tokens |
|-----------|-----------|------|---------|
| Google Sign-In | OIDC | Authorization Code Flow | Access + ID Token |
| GitHub Access via API | OAuth 2.0 | Client Credentials | Access Token |
| Mobile App Authentication | OIDC | Authorization Code with PKCE | Access + ID Token |
| Service-to-Service Communication | OAuth 2.0 | Client Credentials | Access Token |

---

> “OAuth lets apps act on your behalf. OpenID Connect lets apps know who you are.”

---
