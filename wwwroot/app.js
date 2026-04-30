document.addEventListener('DOMContentLoaded', () => {
    // Listen for hash changes
    window.addEventListener('hashchange', handleHashChange);
    
    // Bind global events
    document.getElementById('search-input').addEventListener('input', debounce(handleSearch, 300));
    document.getElementById('hide-system-toggle').addEventListener('change', () => {
        const query = document.getElementById('search-input').value.trim();
        if (query) {
            handleSearch({ target: document.getElementById('search-input') });
        } else {
            fetchDirectory(currentPathState);
        }
    });
    document.getElementById('prevent-traversal-toggle').addEventListener('change', () => {
        const query = document.getElementById('search-input').value.trim();
        if (query) {
            handleSearch({ target: document.getElementById('search-input') });
        } else {
            fetchDirectory(currentPathState);
        }
    });
    document.getElementById('btn-upload').addEventListener('click', openUploadDialog);
    document.getElementById('upload-form').addEventListener('submit', handleUploadSubmit);
    document.getElementById('move-form').addEventListener('submit', handleMoveSubmit);

    // Initial load
    handleHashChange();
});

let currentPathState = '';

function handleHashChange() {
    let hash = window.location.hash;
    let path = '';
    
    if (hash.startsWith('#/')) {
        path = hash.substring(2);
    } else if (hash.startsWith('#')) {
        path = hash.substring(1);
    }
    
    path = decodeURIComponent(path);
    currentPathState = path;
    
    // Clear search bar if navigating
    document.getElementById('search-input').value = '';
    
    fetchDirectory(path);
}

async function fetchDirectory(path) {
    try {
        const hideSystem = document.getElementById('hide-system-toggle').checked;
        const allowTraversal = !document.getElementById('prevent-traversal-toggle').checked;
        const response = await fetch(`/api/files/browse?path=${encodeURIComponent(path)}&hideSystem=${hideSystem}&allowTraversal=${allowTraversal}`);
        if (!response.ok) {
            const errorText = await response.text();
            renderError(errorText || 'Failed to load directory.');
            return;
        }
        const data = await response.json();
        renderUI(data, path);
    } catch (error) {
        console.error('Error fetching directory:', error);
        renderError('A network error occurred while fetching the directory.');
    }
}

async function handleSearch(e) {
    const query = e.target.value.trim();
    if (!query) {
        // If empty, revert to browsing current path
        fetchDirectory(currentPathState);
        return;
    }

    try {
        const hideSystem = document.getElementById('hide-system-toggle').checked;
        const allowTraversal = !document.getElementById('prevent-traversal-toggle').checked;
        const response = await fetch(`/api/files/search?query=${encodeURIComponent(query)}&hideSystem=${hideSystem}&allowTraversal=${allowTraversal}`);
        if (!response.ok) {
            const errorText = await response.text();
            renderError(errorText || 'Search failed.');
            return;
        }
        const data = await response.json();
        renderUI(data, currentPathState, true);
    } catch (error) {
        console.error('Error searching:', error);
        renderError('A network error occurred during search.');
    }
}

function renderUI(data, currentPath, isSearch = false) {
    updateBreadcrumb(currentPath, isSearch);
    updateTotals(data);
    
    const tbody = document.getElementById('file-list');
    tbody.innerHTML = '';
    
    if (!data || data.length === 0) {
        tbody.innerHTML = `<tr><td colspan="4" class="empty-state">${isSearch ? 'No matches found.' : 'This directory is empty.'}</td></tr>`;
        return;
    }

    // Sort: directories first, then files
    data.sort((a, b) => {
        if (a.isDirectory === b.isDirectory) {
            return a.name.localeCompare(b.name);
        }
        return a.isDirectory ? -1 : 1;
    });

    let html = '';
    
    data.forEach(item => {
        const icon = item.isDirectory ? '📁' : '📄';
        const dateStr = item.lastModified ? new Date(item.lastModified).toLocaleString() : '--';
        const sizeStr = item.isDirectory ? '--' : formatBytes(item.size);

        // Escape paths for safe JS passing
        const safePath = item.path.replace(/'/g, "\\'");
        
        let onClickAction = '';
        if (item.isDirectory) {
            const targetHash = `#/${encodeURIComponent(item.path)}`;
            onClickAction = `window.location.hash = '${targetHash}'`;
        } else {
            onClickAction = `console.log('File clicked: ${item.name.replace(/'/g, "\\'")}')`;
        }

        html += `
            <tr class="item-row ${item.isDirectory ? 'is-directory' : 'is-file'}">
                <td class="name-cell" onclick="${onClickAction}">
                    <span class="icon">${icon}</span>
                    <span class="name">${isSearch ? item.path : item.name}</span>
                </td>
                <td class="date-cell">${dateStr}</td>
                <td class="size-cell">${sizeStr}</td>
                <td class="actions-cell">
                    ${!item.isDirectory ? `<button class="btn-icon" title="Download" onclick="downloadFile('${safePath}')">⬇️</button>` : ''}
                    <button class="btn-icon" title="Move/Rename" onclick="openMoveDialog('${safePath}')">✏️</button>
                    <button class="btn-icon" title="Delete" onclick="deleteItem('${safePath}')">🗑️</button>
                </td>
            </tr>
        `;
    });
    
    tbody.innerHTML = html;
}

function updateTotals(data) {
    const totalsDiv = document.getElementById('dynamic-totals');
    if (!data) {
        totalsDiv.innerHTML = '';
        return;
    }
    
    let folders = 0;
    let files = 0;
    let totalSize = 0;
    
    data.forEach(i => {
        if (i.isDirectory) {
            folders++;
        } else {
            files++;
            totalSize += i.size;
        }
    });
    
    totalsDiv.innerHTML = `${folders} Folders &nbsp;|&nbsp; ${files} Files &nbsp;|&nbsp; ${formatBytes(totalSize)} Total Size`;
}

function updateBreadcrumb(currentPath, isSearch) {
    const breadcrumb = document.getElementById('breadcrumb');
    
    if (isSearch) {
        breadcrumb.innerHTML = `<span class="crumb active">Search Results</span>`;
        return;
    }
    
    if (!currentPath) {
        breadcrumb.innerHTML = `<span class="crumb active">Home</span>`;
        return;
    }

    const parts = currentPath.split('/').filter(p => p.length > 0);
    let html = `<a href="#/" class="crumb-link">Home</a>`;
    
    let builtPath = '';
    parts.forEach((part, index) => {
        builtPath += (builtPath.length > 0 ? '/' : '') + part;
        html += `<span class="separator">/</span>`;
        
        if (index === parts.length - 1) {
            html += `<span class="crumb active">${part}</span>`;
        } else {
            html += `<a href="#/${encodeURIComponent(builtPath)}" class="crumb-link">${part}</a>`;
        }
    });
    
    breadcrumb.innerHTML = html;
}

function renderError(message) {
    const tbody = document.getElementById('file-list');
    tbody.innerHTML = `<tr><td colspan="4" class="error-state">${message}</td></tr>`;
}

function formatBytes(bytes, decimals = 2) {
    if (!+bytes) return '0 Bytes';
    const k = 1024;
    const dm = decimals < 0 ? 0 : decimals;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(dm))} ${sizes[i]}`;
}

function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

// === Action Handlers ===

function downloadFile(path) {
    const allowTraversal = !document.getElementById('prevent-traversal-toggle').checked;
    const url = `/api/files/download?path=${encodeURIComponent(path)}&allowTraversal=${allowTraversal}`;
    const a = document.createElement('a');
    a.href = url;
    a.download = '';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
}

async function deleteItem(path) {
    if (!confirm(`Are you sure you want to permanently delete: ${path}?`)) {
        return;
    }
    
    try {
        const allowTraversal = !document.getElementById('prevent-traversal-toggle').checked;
        const response = await fetch(`/api/files?path=${encodeURIComponent(path)}&allowTraversal=${allowTraversal}`, {
            method: 'DELETE'
        });
        
        if (response.ok) {
            fetchDirectory(currentPathState);
        } else {
            const err = await response.text();
            alert('Delete failed: ' + err);
        }
    } catch(e) {
        alert('Error: ' + e.message);
    }
}

function openUploadDialog() {
    document.getElementById('upload-path-text').innerText = `Uploading to: /${currentPathState}`;
    document.getElementById('upload-form').reset();
    document.getElementById('upload-dialog').showModal();
}

async function handleUploadSubmit(e) {
    e.preventDefault();
    const fileInput = document.getElementById('upload-file-input');
    if (!fileInput.files.length) return;
    
    const formData = new FormData();
    formData.append('file', fileInput.files[0]);
    
    // In Swashbuckle bug fix we wrapped in an UploadRequest, but in fetch FormData,
    // how does ASP.NET map it?
    // If we changed to `public async Task<IActionResult> Upload([FromForm] UploadRequest request)`
    // And `UploadRequest { public IFormFile? File }`
    // Then the form data name needs to match the property, which is "file".
    // So formData.append('file', ...) works perfectly!
    
    try {
        const allowTraversal = !document.getElementById('prevent-traversal-toggle').checked;
        const response = await fetch(`/api/files/upload?path=${encodeURIComponent(currentPathState)}&allowTraversal=${allowTraversal}`, {
            method: 'POST',
            body: formData
        });
        
        if (response.ok) {
            document.getElementById('upload-dialog').close();
            fetchDirectory(currentPathState);
        } else {
            const err = await response.text();
            alert('Upload failed: ' + err);
        }
    } catch(err) {
        alert('Error uploading file: ' + err.message);
    }
}

function openMoveDialog(sourcePath) {
    document.getElementById('move-source').value = sourcePath;
    document.getElementById('move-dest').value = sourcePath; // Prepulate to easily rename
    document.getElementById('move-dialog').showModal();
}

async function handleMoveSubmit(e) {
    e.preventDefault();
    const source = document.getElementById('move-source').value;
    const dest = document.getElementById('move-dest').value;
    
    if(source === dest) {
        document.getElementById('move-dialog').close();
        return;
    }
    
    try {
        const allowTraversal = !document.getElementById('prevent-traversal-toggle').checked;
        const response = await fetch('/api/files/move', {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ sourcePath: source, destinationPath: dest, allowTraversal: allowTraversal })
        });
        
        if (response.ok) {
            document.getElementById('move-dialog').close();
            fetchDirectory(currentPathState);
        } else {
            const err = await response.text();
            alert('Move/Rename failed: ' + err);
        }
    } catch(err) {
        alert('Error moving item: ' + err.message);
    }
}
