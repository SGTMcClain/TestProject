# Pseudocode Walkthrough: JS State to .NET File System

This document outlines the high-level flow of data and control between the frontend JavaScript state and the backend `.NET 9` file system operations.

## 1. State Initialization and Routing
The SPA uses URL fragment identifiers (Hashes) to track the current state, avoiding full page reloads.

```text
ON DOMContentLoaded:
    Listen for "hashchange" events on the window object.
    Trigger handleHashChange() to parse the initial state.

FUNCTION handleHashChange:
    READ the URL hash (e.g., "#/projects/MapLarge").
    EXTRACT the path string: "projects/MapLarge".
    SET global state `currentPathState = "projects/MapLarge"`.
    CALL fetchDirectory(currentPathState).
```

## 2. Directory Fetching and Backend Validation
When the frontend requests a directory, it queries the backend API.

```text
// FRONTEND (JS)
FUNCTION fetchDirectory(path):
    READ the UI toggles (hideSystem, allowTraversal).
    SEND HTTP GET to `/api/files/browse?path=[path]&hideSystem=[hideSystem]&allowTraversal=[allowTraversal]`.

// BACKEND (.NET)
ENDPOINT GET /api/files/browse:
    RECEIVE path, hideSystem, allowTraversal.
    
    // The Gatekeeper Check
    CALL GetValidatedPath(path, allowTraversal):
        COMBINE HomeDirectory + path = TargetPath
        RESOLVE TargetPath to AbsolutePath
        IF NOT allowTraversal AND AbsolutePath DOES NOT START WITH HomeDirectory:
            ABORT with "Path Traversal Detected" (Security Feature)
        RETURN AbsolutePath
        
    IF AbsolutePath DOES NOT EXIST:
        RETURN 404.
        
    // High-Performance File Enumeration
    CREATE EnumerationOptions (skip system/hidden files if hideSystem is true).
    YIELD each FileSystemEntry from AbsolutePath iteratively to save memory.
    FORMAT output as JSON array.
    RETURN 200 OK with JSON array.
```

## 3. UI Rendering
Once the backend responds, the frontend renders the data.

```text
// FRONTEND (JS)
ON RECEIVE Directory Data:
    CALL updateBreadcrumbs(currentPathState).
    CALL updateTotals(data) to calculate folder/file counts and size.
    
    SORT data: Folders first, Files second.
    
    FOR EACH item in data:
        CREATE HTML table row.
        IF item is Folder:
            SET OnClick = "Change URL Hash to #/[item.path]".
        IF item is File:
            ADD Download Button pointing to `/api/files/download?path=[item.path]`.
        ADD Move/Delete buttons.
        
    INJECT HTML into the DOM.
```

## 4. File Operations (Example: Delete)
When a user takes action, the state bridges directly to a destructive backend operation.

```text
// FRONTEND (JS)
ON CLICK Delete Button for "projects/MapLarge/image.png":
    PROMPT native confirm("Are you sure?").
    IF user confirms:
        SEND HTTP DELETE to `/api/files?path=projects/MapLarge/image.png`.

// BACKEND (.NET)
ENDPOINT DELETE /api/files:
    CALL GetValidatedPath(path) -> Gatekeeper check.
    IF target is the HomeDirectory root:
        ABORT "Cannot delete root". (Security Feature)
    IF target is a File:
        System.IO.File.Delete(target).
    IF target is a Directory:
        System.IO.Directory.Delete(target, recursive: true).
    RETURN 200 OK.

// FRONTEND (JS)
ON RECEIVE 200 OK:
    CALL fetchDirectory(currentPathState) to re-sync the UI with the file system.
```
