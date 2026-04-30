# Infinite Sky Explorer - Demo Script

**Presenter Note:** This script is designed to guide a demonstration of the Infinite Sky Explorer application to stakeholders or technical peers. It highlights both the user-facing features and the underlying architectural and security decisions.

---

## Introduction

**"Welcome to the Infinite Sky Explorer."**

*(Show the main application window loaded in the browser.)*

"What you are looking at is a custom-built, high-performance file management system. The goal of this project was twofold: to provide a completely secure, sandbox-style backend using the latest **.NET 9** technology, and to deliver a stunning, premium frontend experience without the overhead of heavy JavaScript frameworks like React or Angular."

## Step 1: The Design Philosophy

*(Hover over buttons, show the glassmorphic modal by clicking 'Upload File', then close it.)*

"Before we dive into the functionality, let's talk about the design. We implemented the 'Infinite Sky' aesthetic—a modern, dark-themed, glassmorphic UI. 

**Design Choices:**
- **Zero Frameworks:** We wrote pure Vanilla HTML, CSS, and JavaScript. This means lightning-fast load times and zero dependency bloat.
- **Native Elements:** Notice the modals (like the Upload screen)? We are utilizing the native HTML5 `<dialog>` element. This gives us built-in accessibility and backdrop blurring (`backdrop-filter`) without needing complex third-party modal libraries.
- **Dynamic CSS Variables:** The entire color scheme is controlled via CSS root variables, allowing for rapid reskinning if the agency's branding ever changes."

## Step 2: Browsing and State Management

*(Click on a folder to navigate into it. Point to the URL bar and the breadcrumbs.)*

"As we navigate through the folders, notice how snappy the interface is. 

**Implementation Details:**
- **Hash-Based Routing:** The frontend state is managed entirely by the URL Hash (e.g., `#/projects/InfiniteSky`). A `window.onhashchange` listener detects navigation and dynamically fetches only the JSON data for that specific folder.
- **Memory-Efficient Backend:** On the backend, we aren't loading entire directory structures into memory. We use .NET's `Directory.EnumerateFileSystemEntries`. This yields files one by one as a stream, meaning we can browse a folder with 100,000 files just as fast as a folder with 10 files."

## Step 3: Global Search and Filtering

*(Check/Uncheck the "Hide hidden files" toggle. Then, type a query into the Search Bar.)*

"At the top right, we have our global search and filtering toggles.

**Implementation Details:**
- **Debounced Input:** As I type in the search bar, a 300-millisecond debounce prevents the frontend from spamming the backend with requests. 
- **High-Performance OS Filtering:** When I check 'Hide hidden files', the frontend sends a flag to the API. The .NET backend uses `EnumerationOptions` to filter out `FileAttributes.System` and `FileAttributes.Hidden` at the Operating System level, ensuring hidden files never even enter the application's memory space."

## Step 4: Security and Path Traversal

*(Point out the 'Prevent Path Traversal' toggle.)*

"Because this application directly reads and writes to the hard drive, security is our absolute highest priority. 

**Security Architecture:**
- **The Gatekeeper:** Every single API endpoint (Browse, Search, Delete, Move, Upload, Download) is routed through a central C# method called `GetValidatedPath()`. 
- **Zero-Trust Validation:** This gatekeeper resolves any requested path and strictly verifies that it starts with the administrator-configured `HomeDirectory`. If a malicious user tries to send a request for `../../../etc/passwd`, the gatekeeper instantly detects the path traversal attempt, logs it as a security warning, and drops the request.
- **The Toggle:** For the sake of this demo, we added a toggle to temporarily disable this security feature, allowing administrative access to the entire hard drive, but in production, this strict sandbox is hardcoded."

## Step 5: Destructive Operations

*(Click the 'Move/Rename' pencil icon, rename a file, and then click the 'Delete' trashcan.)*

"Finally, we have full file manipulation capabilities. 

**Implementation Details:**
- **File System Bridges:** When I rename a file, the frontend sends a `PATCH` request with the source and destination. When I delete a file, it sends a `DELETE` request. 
- **Root Protection:** As a final layer of security, the backend contains explicit checks to ensure that the root `HomeDirectory` itself can never be moved or deleted, preventing catastrophic data loss."

---

**"This concludes the walkthrough. We've bridged a highly-optimized .NET 9 API with a beautiful, lightweight Vanilla JS frontend, resulting in a secure and premium File Explorer."**
