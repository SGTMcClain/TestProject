# Infinite Sky Explorer

> [!WARNING]
> **USE WITH CAUTION**: This application grants direct access to the file system of the machine it is running on. It has the capability to read, write, move, and permanently delete files and directories. Ensure you configure the `HomeDirectory` carefully and do not run this in a public or untrusted environment without adding proper authentication.

Infinite Sky Explorer is a high-performance, robust, and secure `.NET 9` Web API coupled with a sleek, zero-framework Single Page Application (SPA). It provides a complete interface for browsing, searching, and managing files within a designated sandbox directory.

## Architectural Overview

This project is built using a monolithic but heavily decoupled architecture:
- **Backend (.NET 9 Web API)**: Handles all direct file system interactions. It uses memory-efficient stream processing and `EnumerationOptions` to handle massive directories without blocking or high memory overhead.
- **Frontend (Vanilla HTML/CSS/JS)**: A "zero-framework" SPA that relies on the native `fetch` API, modern CSS custom properties, and native HTML `<dialog>` elements to deliver a premium, glassmorphic "Infinite Sky" aesthetic.
- **State Management**: The UI uses URL Hash-based routing (`window.onhashchange`) to maintain state. The URL fragment (e.g. `#/projects/InfiniteSky`) acts as the single source of truth for the current directory view.

## AI usage
- To quickly prototype this project I used Gemini to help brainstorm and build out the basic framework. 
- I prompted the AI to work as if I was the lead developer and it was two developers on my team one focusing on the front end and one focused on the backend.
	- I did this because as a development lead most times I would need to take project leads vision and distill it down to workable objectives for the team

## Setup Instructions

1. **Install .NET 9 SDK**: Ensure you have the latest .NET 9 SDK installed on your machine.
2. **Configure Home Directory**: Open `appsettings.json` and set the `FileBrowser:HomeDirectory` value to the absolute path of the directory you want to serve.
    ```json
    "FileBrowser": {
      "HomeDirectory": "/Users/yourusername/Downloads"
    }
    ```
3. **Run the Application**: Open a terminal in the project root and run:
    ```bash
    dotnet run
    ```
4. **Access the App**:
    - **UI**: Open your browser to `http://localhost:<port>/`
    - **API Docs**: Open your browser to `http://localhost:<port>/swagger`

## Security Considerations

- **Zero-Trust Path Traversal Prevention**: Every single endpoint routes through a central `GetValidatedPath()` security gatekeeper. Unless explicitly toggled off by the user, this gatekeeper strictly validates that all requested paths exist *within* the configured `HomeDirectory`. Any attempt to use `../` or absolute paths to escape the sandbox will be rejected and logged.
- **Root Protection**: Operations that modify the directory (like `DELETE` or `MOVE`) have explicit safeguards to prevent modifying or deleting the root `HomeDirectory` itself.
- **Local Host Only**: By default, Kestrel binds to `localhost`. Do not expose this service to the internet without implementing standard JWT/OAuth authentication middleware.
