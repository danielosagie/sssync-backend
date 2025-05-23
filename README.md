# sssync-backend

**sssync-backend** is the .NET backend service for the sssync.app platform. It handles core business logic, data persistence, interactions with external marketplaces, and communication with the frontend applications. It leverages Supabase for database, authentication, storage, and real-time capabilities, and is designed to be deployed on Railway.

## Table of Contents

*   [Overview](#overview)
*   [Getting Started](#getting-started)
    *   [Prerequisites](#prerequisites)
    *   [Installation & Setup](#installation--setup)
    *   [Configuration (Secrets)](#configuration-secrets)
*   [Architecture](#architecture)
    *   [Project Structure](#project-structure)
    *   [core Principles (Clean Architecture)](#core-principles-clean-architecture)
    *   [Ecosystem Interaction (Supabase & Railway)](#ecosystem-interaction-supabase--railway)
*   [Current Functionality & API Endpoints](#current-functionality--api-endpoints)
*   [Development Roadmap / Next Steps](#development-roadmap--next-steps)
*   [Key Features & Implementation](#key-features--implementation)
    *   [1. Authentication (Supabase)](#1-authentication-supabase)
    *   [2. Inventory Synchronization Engine](#2-inventory-synchronization-engine)
    *   [3. Product Recognition Service](#3-product-recognition-service)
*   [Contributing & Adding New Features](#contributing--adding-new-features)
*   [Deployment](#deployment)

## Overview

This backend service provides the core functionality for sssync.app, a multi-channel e-commerce platform designed to simplify inventory management and enable inter-seller marketplace features. It acts as the central hub connecting various online marketplaces (Shopify, Square, Amazon, etc.) and user interfaces (web app, mobile app).


### Prerequisites

Ensure you have the following tools installed on your system (macOS instructions provided where applicable):

1.  **.NET SDK:** Version 8.0 or later.
    *   *macOS (Homebrew):* `brew install dotnet-sdk`
    *   *Official Installer:* [Download .NET](https://dotnet.microsoft.com/download)
2.  **Supabase CLI:** For managing local Supabase environment, migrations, and edge functions.
    *   *macOS (Homebrew):* `brew install supabase/tap/supabase`
    *   *Other Systems:* See [Supabase CLI Docs](https://supabase.com/docs/guides/cli)
3.  **Docker:** Required by the Supabase CLI to run the local development stack (Postgres, GoTrue, etc.).
    *   *macOS/Windows:* Install Docker Desktop.
    *   *Linux:* Install Docker Engine.
4.  **Git:** For version control.
    *   *macOS (Homebrew):* `brew install git` (usually pre-installed)
5.  **ENV** Create a `.env` file in the root of the repository and add the following variables:
    *   `SUPABASE_URL`
    *   `SUPABASE_ANON_KEY`
    *   `SUPABASE_SERVICE_ROLE_KEY` (for Railway deployment)

**Configure Git Ignore (`.gitignore`)**
*`.gitignore` Essentials**

Ensure your `.gitignore` file (at the root of the repository) includes at least the following to prevent committing build artifacts, user settings, and potentially sensitive files:

```gitignore
# .NET Core build outputs
[Bb]in/
[Oo]bj/

# IDE specific files
.idea/
.vs/
*.suo
*.[Cc]ache
*.user
*.userosscache

# Supabase local temp files
supabase/.temp/

# User Secrets file ID (prevents committing the ID itself)
**/secrets.json

# Rider specific
*.sln.iml

# Windows thumbnail cache
Thumbs.db

# Secrets
* appsettings.Development.json #If you ever accidentally put secrets here
* .env #If you ever accidentally put secrets here
```
**Important:** Do **NOT** add `appsettings.json` or `supabase/migrations/` to `.gitignore`.


## Architecture

This project follows the principles of **Clean Architecture** (also known as Onion Architecture) to ensure separation of concerns, testability, and maintainability.

### Project Structure

```
sssync-backend/
├── src/ # Main source code directory
│ ├── api/ # ASP.NET core Web API project (Controllers, Middleware, DTOs, Startup)
│ ├── core/ # Domain Logic (Entities, Interfaces, Business Rules - NO external dependencies)
│ ├── infrastructure/ # Implementation Details (Supabase Client, DB Context, External API Clients)
│ └── workers/ # Background Services (Hosted Services like Realtime listeners, Scheduled Tasks)
├── supabase/ # Supabase-specific configuration managed by the Supabase CLI
│ ├── migrations/ # Database schema migration files (SQL)
│ └── functions/ # Edge Functions code (Deno/TypeScript) - Optional, for specific tasks
├── .gitignore # Git ignore rules
├── appsettings.json # Base configuration
├── appsettings.Development.json # Development-specific configuration (DO NOT COMMIT SECRETS)
├── sssync-backend.sln # .NET Solution file grouping all projects
└── README.md # This file
```


### Core Principles (Clean Architecture)

*   **Dependencies Flow Inwards:** `api`, `infrastructure`, and `workers` depend on `core`. `core` depends on *nothing* else within the `src` directory.
*   **`core`:** Contains the heart of the application:
    *   **Entities:** Plain C# objects representing the fundamental concepts (Product, User, Order, etc.).
    *   **Interfaces:** Defines contracts for repositories (`IProductRepository`), external services (`IMarketplaceConnector`, `IAuthService`), and domain logic (`INormalizationService`).
    *   **Domain Services/Logic:** Business rules and orchestration logic that depend only on core interfaces and entities.
*   **`infrastructure`:** Contains implementations for interfaces defined in `core`.
    *   **Repositories:** Implement data access logic using Supabase Client SDK or EF core against the Supabase Postgres database.
    *   **External Service Clients:** Code to interact with Supabase Auth, Storage, Realtime, and third-party APIs (Shopify, Square, Vision APIs).
*   **`api`:** The entry point for HTTP requests.
    *   **Controllers:** Handle incoming requests, validate input (DTOs), call services (often defined by interfaces in `core`), and format responses. Thin layer, minimal logic.
    *   **Middleware:** Handles cross-cutting concerns like authentication, logging, error handling.
*   **`workers`:** Background processes running independently of user requests.
    *   Implement `IHostedService` for tasks like scheduled syncs, processing message queues, or listening to real-time events.

### Ecosystem Interaction (Supabase & Railway)

*   **Supabase:** Acts as the Backend-as-a-Service (BaaS) provider.
    *   **Database:** Supabase provides a managed **PostgreSQL** database. The .NET backend interacts with it either via the `Supabase.Client` SDK (good for simple operations, realtime) or a standard ORM like Entity Framework core (`Npgsql.EntityFrameworkcore.PostgreSQL`). Migrations are managed via the Supabase CLI (`supabase/migrations`).
    *   **Authentication:** Supabase handles user sign-up, sign-in, password management, and OAuth. The .NET backend validates JWTs issued by Supabase (`Supabase.Client.Auth.GetUser(token)`) usually via middleware. User data resides within Supabase's `auth.users` schema.
    *   **Storage:** Used for storing files like product images. The backend can generate signed URLs for uploads/downloads or interact directly using the `Supabase.Storage` client.
    *   **Realtime:** Listens to database changes via WebSockets (`Supabase.Realtime`). The `RealtimeWorker` in the `workers` project is a typical place to handle these events.
    *   **Edge Functions:** (Optional) Deno/TypeScript functions deployed globally via Supabase for low-latency tasks. The .NET backend can invoke these using `Supabase.Functions`.
*   **Railway:** A platform for deploying applications. This .NET backend can be containerized (using a Dockerfile) and deployed to Railway, which connects to the *production* Supabase instance via environment variables containing the production Supabase URL and keys.

## Current Functionality & API Endpoints

As of the current commit, the backend has the following capabilities implemented:

*   **Shopify OAuth:**
    *   `GET /api/auth/shopify/initiate?shop={domain}`: Starts the OAuth flow. Requires JWT auth.
    *   `GET /api/auth/shopify/callback`: Handles the callback from Shopify, exchanges the code for an access token, and saves the connection to the database.
*   **Platform Connection Management:**
    *   `GET /api/connections`: Lists the user's currently saved platform connections (e.g., Shopify). Requires JWT auth.
    *   `DELETE /api/connections/{platformName}`: Deletes a specific platform connection for the user. Requires JWT auth.
*   **Data Fetching (Shopify):**
    *   The sync process can fetch Locations, Products, Variants, and Inventory Levels from a connected Shopify store.
*   **Data Persistence (Supabase):**
    *   Fetched data is used to create/update corresponding records (Products, Variants, Locations, Inventory Levels) in the Supabase database via repositories.
    *   Mappings between internal GUIDs and Shopify IDs are saved.
*   **API Sync Trigger:**
    *   `POST /api/sync/trigger`: Manually starts the sync process for the authenticated user (fire-and-forget). Requires JWT auth.
*   **Background Sync Worker:**
    *   A background service runs periodically, identifies users with active connections, and triggers the sync process for them.
*   **Shopify Write Operations:**
    *   Methods exist within `ShopifyService` to create/update products/locations and update inventory levels on Shopify.

**Current Limitations:**

*   **Change Detection:** The core logic to compare data between the internal database and fetched platform data (`SyncService.DetectChanges`) is **not implemented**.
*   **Update Pushing:** Because change detection is missing, no updates are currently pushed back to Shopify, even though the write methods exist.
*   **Conflict Resolution:** No rules are defined or implemented for handling conflicting data between platforms (e.g., price differs between Shopify and Square).
*   **Other Platforms:** Clover and Square connectors/logic are not yet implemented.
*   **Error Handling:** Basic error logging exists, but robust handling (retries, user notifications, disabling failing connections) needs improvement.
*   **Testing:** Limited automated tests exist.

## Development Roadmap / Next Steps

1.  **Configure Secrets:** Ensure User Secrets (local) and environment variables (deployment) are correctly set up. **(BLOCKER)**
2.  **Implement Core Sync Logic (`SyncService.DetectChanges`):**
    *   Define the "Source of Truth" strategy (e.g., which platform's data wins for inventory, price, title?). Start simple (e.g., Shopify is SoT for inventory, internal DB wins for title after first sync).
    *   Implement the comparison logic to identify discrepancies between the consolidated data (from DB) and newly fetched platform data.
    *   Generate the `PlatformUpdateAction` list based on detected differences and SoT rules.
3.  **Refine Update Pushing (`SyncService.PushUpdatesToPlatformsAsync`):**
    *   Ensure it correctly handles the `PlatformUpdateAction` list generated by `DetectChanges`.
    *   Add more robust error handling for failed API calls during the push. Consider retry logic or flagging items that failed to update.
4.  **Refine Consolidation (`SyncService.ConsolidateAndMapDataAsync`):**
    *   Add logic to merge/update properties on *existing* internal records if the fetched platform data is considered newer or the source of truth.
5.  **Testing:**
    *   Write unit tests for `SyncService.DetectChanges` logic.
    *   Write integration tests for `ShopifyService` write operations against a Shopify development store.
    *   Test the end-to-end sync flow locally using Postman or the Swagger UI after implementing change detection.
6.  **Add Specific API Update Endpoints (Optional but Recommended):** Create endpoints like `PUT /api/products/{internalId}` or `PUT /api/variants/{internalId}/inventory` to allow the frontend to trigger specific, immediate updates outside the main sync cycle if needed.
7.  **Implement Clover/Square:** Add `CloverService`, `SquareService`, implement `IPlatformIntegrationService`, handle their auth, and register them.
8.  **Frontend Integration:** Connect the React Native app to the API endpoints for auth, connection management, and displaying sync status/data.

## Key Features & Implementation

This section details where the logic for major features resides within the architecture.

### 1. Authentication (Supabase)

*   **Goal:** Authenticate users via Supabase when they interact with your API.
*   **Location:**
    *   **`core`:** `IAuthService` interface, `User` record (optional).
    *   **`infrastructure`:** `SupabaseAuthService : IAuthService` (using `Supabase.Client.Auth`).
    *   **`api`:** `SupabaseAuthenticationMiddleware`, `AuthController` (optional), `[Authorize]` attributes or manual checks in controllers.
*   **Flow:**
    1.  Frontend uses Supabase JS client for Sign In/Sign Up, obtaining a JWT.
    2.  Frontend sends JWT in `Authorization: Bearer <token>` header with API requests.
    3.  `SupabaseAuthenticationMiddleware` (`api`) intercepts the request.
    4.  Middleware calls `IAuthService.GetUserFromTokenAsync()` (`core` interface).
    5.  `SupabaseAuthService` (`infrastructure`) implementation uses `_supabaseClient.Auth.GetUser(token)` to validate the token with Supabase.
    6.  If valid, user info is attached to `HttpContext`.
    7.  Controller action proceeds, potentially checking for the authenticated user.
*   **Don't:** Store passwords in the backend DB; manually validate JWTs (unless necessary for specific reasons).

### 2. Inventory Synchronization Engine

*   **Goal:** Pull data from marketplaces, normalize it, store a master record, and push updates.
*   **Location:**
    *   **`core`:** Entities (`Product`, `MarketplaceConnection`, etc.), Interfaces (`IMarketplaceConnector`, `IProductRepository`, `INormalizationService`, `ISyncOrchestrationService`), Business Logic implementations (`NormalizationService`, `SyncOrchestrationService`).
    *   **`infrastructure`:** Marketplace Connectors (`ShopifyConnector`, `SquareConnector`), Repositories (`ProductRepository` using EF core/Supabase Client), Supabase DB interaction logic.
    *   **`api`:** Controllers (`MarketplaceConnectionsController`, `SyncController`) to trigger/monitor syncs.
    *   **`workers`:** `TimedSyncWorker` (scheduled syncs), `RealtimeWorker` (push updates on master record changes), Job Queue Processor (for long syncs).
*   **Flow (Simplified Pull Sync):**
    1.  `TimedSyncWorker` (`workers`) or API call triggers `ISyncOrchestrationService.StartSync(connectionId)` (`core` interface).
    2.  `SyncOrchestrationService` (`core` logic) retrieves `MarketplaceConnection` details via `IMarketplaceConnectionRepository` (`core` interface, implemented in `infrastructure`).
    3.  It determines the correct `IMarketplaceConnector` (`core` interface) based on the connection type.
    4.  It calls `IMarketplaceConnector.FetchProductsAsync()` (implemented in `infrastructure`, e.g., `ShopifyConnector`).
    5.  The connector (`infrastructure`) uses the specific marketplace SDK/API to fetch raw product data.
    6.  Raw data is passed to `INormalizationService.StandardizeProduct()` (`core` interface/logic).
    7.  Normalized data (matching `core` entities) is saved to the master database using `IProductRepository` / `IInventoryRepository` (`core` interfaces, implemented in `infrastructure`).
*   **Don't:** Put marketplace SDKs in `core`; mix DB code directly in `core` services.



### 3. Product Recognition Service

*   **Goal:** Extract info from an image, search marketplaces, and help create a new product listing.
*   **Location:**
    *   **`core`:** Entities (`ProductCandidate`, `ImageMetadata`), Interfaces (`IVisionService`, `IExternalProductSearcher`, `IProductRecognitionService`), Business Logic (`ProductRecognitionService`).
    *   **`infrastructure`:** Vision Service implementations (`GoogleVisionService`), Searcher implementations (`AmazonProductSearcher`), Supabase Storage interaction (optional, for image handling).
    *   **`api`:** `ProductRecognitionController` (endpoints for image upload and candidate creation).
    *   **`workers`:** Less likely involved unless processing is asynchronous.
*   **Flow (Simplified):**
    1.  Mobile/Web App uploads image to `POST /api/recognize/image` (`api`).
    2.  `ProductRecognitionController` (`api`) receives image data (`byte[]` or `Stream`).
    3.  Controller calls `IProductRecognitionService.RecognizeFromImageAsync(imageData)` (`core` interface).
    4.  `ProductRecognitionService` (`core` logic) calls `IVisionService.ExtractDataFromImageAsync(imageData)` (`core` interface).
    5.  The `IVisionService` implementation (`infrastructure`, e.g., `GoogleVisionService`) calls the external Vision API.
    6.  Extracted data (`ImageMetadata`) is returned to `ProductRecognitionService`.
    7.  `ProductRecognitionService` uses extracted text/barcode to call `IExternalProductSearcher.SearchAsync()` (`core` interface) for relevant marketplaces.
    8.  `IExternalProductSearcher` implementations (`infrastructure`) call external marketplace search APIs.
    9.  `ProductRecognitionService` aggregates search results into `ProductCandidate` (`core` entity).
    10. Controller (`api`) returns the `ProductCandidate` to the frontend.
    11. User potentially selects a candidate and calls `POST /api/recognize/create` (`api`).
    12. Controller calls a service (maybe `IProductService` or similar) which uses `IProductRepository` (`core` interface, implemented in `infrastructure`) to save the new product to the master database.
*   **Don't:** Hardcode external API keys in `core`; assume image formats in `core`.



## Contributing & Adding New Features

When adding new features or modifying existing ones, please adhere to the Clean Architecture principles:

1.  **Define in `core`:**
    *   Start by defining necessary **Entities** and **Interfaces** in the `sssync-backend.core` project.
    *   If adding new business logic, create domain services within `core` that depend *only* on `core` interfaces/entities.
2.  **Implement in `infrastructure`:**
    *   Create implementations for the new interfaces defined in `core` within the `sssync-backend.infrastructure` project.
    *   This is where database access code (Repositories), external API calls (Connectors, Services), and other implementation details belong.
    *   Add any required NuGet packages (like SDKs for external services) to the `sssync-backend.infrastructure.csproj` file.
3.  **Expose via `api` (if needed):**
    *   If the feature needs to be triggered by an HTTP request, add a new **Controller** or modify an existing one in the `sssync-backend.api` project.
    *   Controllers should be "thin" – they receive requests, validate input (using Data Transfer Objects - DTOs), call the appropriate service (using interfaces defined in `core`), and return responses.
    *   Add any necessary DTOs in the `api` project.
4.  **Create `workers` (if needed):**
    *   If the feature involves background processing, scheduled tasks, or real-time event handling, create a new `IHostedService` implementation in the `sssync-backend.workers` project.
5.  **Dependency Injection:**
    *   Register your new services and their interfaces in `Program.cs` (or `Startup.cs`) within the `api` project so they can be injected where needed. Use appropriate lifetimes (Singleton, Scoped, Transient).
6.  **Testing:**
    *   Write unit tests for logic in `core` (these should be easy as there are no external dependencies).
    *   Write integration tests for `infrastructure` components and `api` endpoints where appropriate.

## Deployment

*   This application is designed to be deployed to **Railway**.
*   A `Dockerfile` should be created to containerize the application.
*   Environment variables on Railway must be configured with the **production** Supabase URL, anon key, and service role key.
*   Ensure the Railway service builds the container and runs the `sssync-backend.api.dll`.
