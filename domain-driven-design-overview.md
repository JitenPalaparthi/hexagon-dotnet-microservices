# 🧭 Domain-Driven Design (DDD) — Concepts, Principles, and Use Case

## 1. What Is Domain-Driven Design?
**Domain-Driven Design (DDD)** is a strategic and tactical approach to building software systems that closely reflect real-world business domains.  
It was introduced by **Eric Evans** in *“Domain-Driven Design: Tackling Complexity in the Heart of Software”* (2003).

DDD helps developers and business experts **speak the same language**, ensuring that the system’s structure and behavior directly mirror the organization’s core business concepts.

> **Core idea:** Software should model the *business domain*, not just data or infrastructure.

---

## 2. Key Principles of DDD

| Principle | Description | Example |
|------------|--------------|----------|
| **Ubiquitous Language** | A shared language used consistently by developers and domain experts. | Use terms like *Order*, *Invoice*, *Shipment* instead of *tbl_order* or *trans_data*. |
| **Bounded Context** | Defines the boundary within which a particular model is valid and consistent. | The *Order Context* has its own meaning of “Order”, separate from the *Billing Context*. |
| **Entities** | Objects identified by a unique identity, not just attributes. | `Customer` or `Order` remains the same even if details change. |
| **Value Objects** | Objects defined only by their values. They are immutable and have no identity. | `Address` (Street, City, ZIP) — two identical addresses are considered equal. |
| **Aggregates & Roots** | A cluster of related entities and value objects treated as a single unit. | `Order` (root) contains `OrderItems` (children). Only `Order` is accessed directly. |
| **Repositories** | Abstractions for retrieving and storing aggregates. | `OrderRepository` hides database logic behind an interface. |
| **Domain Services** | Logic that doesn’t belong to any specific entity or value object. | `PaymentProcessor` handles cross-aggregate operations. |
| **Domain Events** | Represent something that has happened within the domain. | `OrderPlaced`, `PaymentReceived`. |
| **Anti-Corruption Layer (ACL)** | Isolates different bounded contexts, translating data between them. | `BillingContext` uses ACL to map external “Order” objects to its own model. |

---

## 3. The Layered Architecture

A DDD-based system is commonly divided into **four logical layers**:

```
┌──────────────────────────────────────────────┐
│  Presentation Layer (API / UI)               │
│  - REST, GraphQL, or Web UI                  │
└──────────────────────────────────────────────┘
┌──────────────────────────────────────────────┐
│  Application Layer (Use Cases)               │
│  - Coordinates domain logic and transactions │
└──────────────────────────────────────────────┘
┌──────────────────────────────────────────────┐
│  Domain Layer (Business Logic Core)          │
│  - Entities, Value Objects, Domain Services  │
└──────────────────────────────────────────────┘
┌──────────────────────────────────────────────┐
│  Infrastructure Layer                        │
│  - Database, Message Brokers, File Systems   │
└──────────────────────────────────────────────┘
```

**Guiding rule:**  
The Domain Layer should have *no dependency* on infrastructure or frameworks.

---

## 4. Building Blocks of the Domain Layer

### 4.1 Entities
Objects that have a unique identity (`Id`) and persist over time even if their data changes.

### 4.2 Value Objects
Immutable objects identified by their value. Example: Address or Money.

### 4.3 Aggregates and Aggregate Roots
A set of entities and value objects treated as one unit for data consistency. Example: `Order` is an aggregate root that owns `OrderItems`.

### 4.4 Domain Services
Contain business logic that doesn't belong to a single entity. Example: Payment processing.

### 4.5 Repositories
Abstractions that handle persistence of aggregates. Example: OrderRepository.

### 4.6 Domain Events
Represent something that happened within the domain. Example: OrderPlaced, PaymentReceived.

---

## 5. Strategic Design: The “Big Picture”

### Bounded Contexts
Each bounded context has its own model and data. For example:

```
E-Commerce System
│
├── Catalog Context
│   ├── Product
│   ├── Category
│
├── Order Context
│   ├── Order
│   ├── Payment
│
└── Customer Context
    ├── Customer
    ├── Address
```

### Context Mapping
Defines how bounded contexts interact.  
Examples: Shared Kernel, Customer-Supplier, Anti-Corruption Layer.

---

## 6. Example Use Case — Online Ordering System

### Scenario
An e-commerce business needs to manage Orders, Payments, Shipments, and Customers.

### Concepts Applied
| Concept | Example |
|----------|----------|
| **Bounded Contexts** | OrderContext, PaymentContext, ShippingContext, CustomerContext |
| **Ubiquitous Language** | Everyone uses the same business terms. |
| **Entities** | Order, Customer, Payment |
| **Value Objects** | Address, Money |
| **Aggregates** | Order (root) → OrderItems |
| **Repositories** | OrderRepository |
| **Domain Events** | OrderPlaced, PaymentReceived, ShipmentCreated |

### Workflow
1. Customer places an order → `OrderPlaced` event emitted.  
2. Payment service processes payment → `PaymentReceived` event emitted.  
3. Shipping service schedules shipment → `ShipmentCreated` event emitted.

Each service owns its own data and reacts to domain events from others.

---

## 7. DDD in Microservices

DDD maps naturally to microservice architecture:

| DDD Concept | Microservice Equivalent |
|--------------|-------------------------|
| Bounded Context | Microservice |
| Aggregate | Service’s internal model |
| Repository | Database adapter |
| Domain Event | Message/Event |
| Anti-Corruption Layer | API adapter between services |

---

## 8. Benefits of DDD

- Aligns code with business language.  
- Makes complex systems maintainable.  
- Enables microservice scalability.  
- Encourages modular design.  
- Supports team autonomy via bounded contexts.

---

## 9. When to Apply DDD

**Use when:**
- Domain is complex and business-driven.  
- Frequent changes in business logic.  
- Long-term maintainability is crucial.

**Avoid when:**
- Application is simple CRUD or data-centric.  
- Short-lived or proof-of-concept projects.

---

## 10. Summary

| Concept | Description |
|----------|-------------|
| Ubiquitous Language | Shared vocabulary between domain experts and developers. |
| Bounded Context | Logical domain boundary. |
| Entity | Object with unique identity. |
| Value Object | Immutable by value. |
| Aggregate | Consistency boundary. |
| Repository | Data access abstraction. |
| Service | Domain operation. |
| Event | Business occurrence. |
| ACL | Model translator between contexts. |

---

## 11. Final Thought

> “DDD is not about frameworks or patterns; it’s about understanding the business deeply and reflecting it in software.”

---
