// Custom JavaScript for Device Operations API Swagger UI
(function() {
    'use strict';

    // Wait for Swagger UI to load
    window.addEventListener('load', function() {
        setTimeout(initializeCustomizations, 1000);
    });

    function initializeCustomizations() {
        addPerformanceMonitoring();
        addRequestInterceptors();
        addResponseEnhancements();
        addKeyboardShortcuts();
        addApiUsageTracking();
        addCustomValidation();
        console.log('Device Operations API - Custom Swagger UI initialized');
    }

    // Add performance monitoring to requests
    function addPerformanceMonitoring() {
        const originalFetch = window.fetch;
        window.fetch = function(...args) {
            const startTime = performance.now();
            const url = args[0];
            
            return originalFetch.apply(this, args)
                .then(response => {
                    const endTime = performance.now();
                    const duration = Math.round(endTime - startTime);
                    
                    // Add performance metrics to response
                    addPerformanceMetrics(url, duration, response.status);
                    return response;
                })
                .catch(error => {
                    const endTime = performance.now();
                    const duration = Math.round(endTime - startTime);
                    addPerformanceMetrics(url, duration, 'ERROR');
                    throw error;
                });
        };
    }

    function addPerformanceMetrics(url, duration, status) {
        // Find the relevant operation block
        const operationBlocks = document.querySelectorAll('.opblock');
        operationBlocks.forEach(block => {
            const urlElement = block.querySelector('.opblock-summary-path');
            if (urlElement && url.includes(urlElement.textContent)) {
                let metricsDiv = block.querySelector('.performance-metrics');
                if (!metricsDiv) {
                    metricsDiv = document.createElement('div');
                    metricsDiv.className = 'performance-metrics';
                    block.appendChild(metricsDiv);
                }
                
                const timestamp = new Date().toLocaleTimeString();
                metricsDiv.innerHTML = `
                    <strong>Last Request:</strong> ${timestamp} | 
                    <strong>Duration:</strong> ${duration}ms | 
                    <strong>Status:</strong> ${status}
                `;
                
                // Add color coding based on performance
                if (duration < 100) {
                    metricsDiv.style.borderColor = '#10b981'; // Green
                } else if (duration < 500) {
                    metricsDiv.style.borderColor = '#f59e0b'; // Yellow
                } else {
                    metricsDiv.style.borderColor = '#ef4444'; // Red
                }
            }
        });
    }

    // Add request interceptors for common headers
    function addRequestInterceptors() {
        // Auto-add correlation ID to requests
        const requestInterceptor = (request) => {
            if (!request.headers['x-correlation-id']) {
                request.headers['x-correlation-id'] = generateCorrelationId();
            }
            
            // Add timestamp for request tracking
            request.headers['x-request-timestamp'] = new Date().toISOString();
            
            return request;
        };

        // Try to hook into SwaggerUI's request interceptor if available
        if (window.ui && window.ui.getConfigs) {
            const config = window.ui.getConfigs();
            config.requestInterceptor = requestInterceptor;
        }
    }

    function generateCorrelationId() {
        return 'req-' + Math.random().toString(36).substr(2, 9) + '-' + Date.now().toString(36);
    }

    // Enhance response display
    function addResponseEnhancements() {
        // Monitor for response sections being added
        const observer = new MutationObserver(function(mutations) {
            mutations.forEach(function(mutation) {
                mutation.addedNodes.forEach(function(node) {
                    if (node.nodeType === Node.ELEMENT_NODE) {
                        enhanceResponseDisplay(node);
                    }
                });
            });
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    function enhanceResponseDisplay(element) {
        // Look for response code elements
        const responseElements = element.querySelectorAll('.response .response-col_status');
        responseElements.forEach(responseElement => {
            const statusCode = responseElement.textContent.trim();
            addStatusCodeStyling(responseElement, statusCode);
        });

        // Look for JSON response bodies
        const jsonElements = element.querySelectorAll('.response-col_description .highlight-code');
        jsonElements.forEach(jsonElement => {
            addJsonFormatting(jsonElement);
        });
    }

    function addStatusCodeStyling(element, statusCode) {
        const code = parseInt(statusCode);
        let color = '#6b7280'; // Default gray
        
        if (code >= 200 && code < 300) {
            color = '#10b981'; // Green for success
        } else if (code >= 300 && code < 400) {
            color = '#f59e0b'; // Yellow for redirects
        } else if (code >= 400 && code < 500) {
            color = '#ef4444'; // Red for client errors
        } else if (code >= 500) {
            color = '#dc2626'; // Dark red for server errors
        }
        
        element.style.color = color;
        element.style.fontWeight = 'bold';
    }

    function addJsonFormatting(element) {
        // Add copy button for JSON responses
        if (!element.querySelector('.copy-json-btn')) {
            const copyBtn = document.createElement('button');
            copyBtn.textContent = 'Copy JSON';
            copyBtn.className = 'copy-json-btn btn';
            copyBtn.style.cssText = `
                position: absolute;
                top: 5px;
                right: 5px;
                background: #4f46e5;
                color: white;
                border: none;
                padding: 5px 10px;
                border-radius: 4px;
                font-size: 12px;
                cursor: pointer;
            `;
            
            copyBtn.onclick = function() {
                const jsonText = element.textContent || element.innerText;
                navigator.clipboard.writeText(jsonText).then(() => {
                    copyBtn.textContent = 'Copied!';
                    setTimeout(() => {
                        copyBtn.textContent = 'Copy JSON';
                    }, 2000);
                });
            };
            
            element.style.position = 'relative';
            element.appendChild(copyBtn);
        }
    }

    // Add keyboard shortcuts
    function addKeyboardShortcuts() {
        document.addEventListener('keydown', function(e) {
            // Ctrl/Cmd + K to focus search
            if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
                e.preventDefault();
                const searchInput = document.querySelector('.filter input');
                if (searchInput) {
                    searchInput.focus();
                }
            }
            
            // Ctrl/Cmd + E to expand all sections
            if ((e.ctrlKey || e.metaKey) && e.key === 'e') {
                e.preventDefault();
                toggleAllSections(true);
            }
            
            // Ctrl/Cmd + R to collapse all sections
            if ((e.ctrlKey || e.metaKey) && e.key === 'r') {
                e.preventDefault();
                toggleAllSections(false);
            }
        });

        // Add keyboard shortcut help
        addKeyboardShortcutHelp();
    }

    function toggleAllSections(expand) {
        const buttons = document.querySelectorAll('.opblock-summary');
        buttons.forEach(button => {
            const section = button.closest('.opblock');
            const isExpanded = section.classList.contains('is-open');
            
            if (expand && !isExpanded) {
                button.click();
            } else if (!expand && isExpanded) {
                button.click();
            }
        });
    }

    function addKeyboardShortcutHelp() {
        const helpDiv = document.createElement('div');
        helpDiv.id = 'keyboard-shortcuts-help';
        helpDiv.style.cssText = `
            position: fixed;
            bottom: 20px;
            right: 20px;
            background: rgba(0, 0, 0, 0.8);
            color: white;
            padding: 15px;
            border-radius: 6px;
            font-size: 12px;
            z-index: 9999;
            display: none;
        `;
        helpDiv.innerHTML = `
            <div><strong>Keyboard Shortcuts:</strong></div>
            <div>Ctrl/Cmd + K: Focus search</div>
            <div>Ctrl/Cmd + E: Expand all</div>
            <div>Ctrl/Cmd + R: Collapse all</div>
            <div>Press ? to toggle this help</div>
        `;
        document.body.appendChild(helpDiv);

        document.addEventListener('keydown', function(e) {
            if (e.key === '?' && !e.ctrlKey && !e.metaKey) {
                e.preventDefault();
                const help = document.getElementById('keyboard-shortcuts-help');
                help.style.display = help.style.display === 'none' ? 'block' : 'none';
            }
        });
    }

    // Add API usage tracking
    function addApiUsageTracking() {
        const usageData = JSON.parse(localStorage.getItem('api-usage') || '{}');
        
        // Track endpoint usage
        const originalFetch = window.fetch;
        window.fetch = function(url, options) {
            const endpoint = extractEndpoint(url);
            if (endpoint) {
                usageData[endpoint] = (usageData[endpoint] || 0) + 1;
                localStorage.setItem('api-usage', JSON.stringify(usageData));
                updateUsageDisplay();
            }
            return originalFetch.apply(this, arguments);
        };
    }

    function extractEndpoint(url) {
        try {
            const urlObj = new URL(url, window.location.origin);
            const path = urlObj.pathname;
            // Remove /api/ prefix and normalize
            return path.replace(/^\/api\//, '').replace(/\/[^\/]+$/g, '/*');
        } catch (e) {
            return null;
        }
    }

    function updateUsageDisplay() {
        // Could add usage statistics to the UI if needed
        console.log('API Usage updated:', JSON.parse(localStorage.getItem('api-usage') || '{}'));
    }

    // Add custom validation for common patterns
    function addCustomValidation() {
        // Monitor form inputs for validation
        const observer = new MutationObserver(function(mutations) {
            mutations.forEach(function(mutation) {
                mutation.addedNodes.forEach(function(node) {
                    if (node.nodeType === Node.ELEMENT_NODE) {
                        addValidationToInputs(node);
                    }
                });
            });
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    function addValidationToInputs(element) {
        const inputs = element.querySelectorAll('input[type="text"], textarea');
        inputs.forEach(input => {
            const paramName = input.closest('.parameter')?.querySelector('.parameter__name')?.textContent;
            
            if (paramName) {
                addParameterValidation(input, paramName);
            }
        });
    }

    function addParameterValidation(input, paramName) {
        if (paramName.includes('deviceId') || paramName.includes('idDevice')) {
            input.addEventListener('blur', function() {
                if (this.value && !this.value.match(/^(cpu|gpu)-\d+$/)) {
                    showValidationError(this, 'Device ID should be in format: cpu-0, gpu-0, etc.');
                } else {
                    clearValidationError(this);
                }
            });
        }
        
        if (paramName.includes('sessionId') || paramName.includes('allocationId')) {
            input.addEventListener('blur', function() {
                if (this.value && !this.value.match(/^[a-f0-9-]{36}$/)) {
                    showValidationError(this, 'ID should be a valid UUID format');
                } else {
                    clearValidationError(this);
                }
            });
        }
    }

    function showValidationError(input, message) {
        clearValidationError(input);
        
        const errorDiv = document.createElement('div');
        errorDiv.className = 'validation-error';
        errorDiv.style.cssText = `
            color: #ef4444;
            font-size: 12px;
            margin-top: 5px;
            padding: 5px;
            background: #fef2f2;
            border: 1px solid #fca5a5;
            border-radius: 4px;
        `;
        errorDiv.textContent = message;
        
        input.parentNode.appendChild(errorDiv);
        input.style.borderColor = '#ef4444';
    }

    function clearValidationError(input) {
        const errorDiv = input.parentNode.querySelector('.validation-error');
        if (errorDiv) {
            errorDiv.remove();
        }
        input.style.borderColor = '';
    }

    // Add utility functions to window for debugging
    window.DeviceOperationsAPI = {
        getUsageStats: () => JSON.parse(localStorage.getItem('api-usage') || '{}'),
        clearUsageStats: () => localStorage.removeItem('api-usage'),
        expandAll: () => toggleAllSections(true),
        collapseAll: () => toggleAllSections(false),
        version: '1.0.0'
    };
})();
