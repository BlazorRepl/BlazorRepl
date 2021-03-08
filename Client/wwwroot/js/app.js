window.App = window.App || (function () {
    return {
        reloadIFrame: function (id, newSrc) {
            const iFrame = document.getElementById(id);
            if (!iFrame) {
                return;
            }

            if (newSrc) {
                // There needs to be some change so the iFrame is actually reloaded
                iFrame.src = '';
                setTimeout(() => iFrame.src = newSrc);
            } else {
                iFrame.contentWindow.location.reload();
            }
        },
        changeDisplayUrl: function (url) {
            if (!url) {
                return;
            }

            window.history.pushState(null, null, url);
        },
        getUrlFragmentValue: function () {
            const hash = window.location.hash;
            const hashValue = hash && hash.substr(1);
            if (!hashValue) {
                return null;
            }

            return BINDING.js_string_to_mono_string(hashValue);
        },
        copyToClipboard: function (text) {
            if (!text) {
                return;
            }

            const input = document.createElement('textarea');
            input.style.top = '0';
            input.style.left = '0';
            input.style.position = 'fixed';
            input.innerHTML = text;
            document.body.appendChild(input);
            input.select();
            document.execCommand('copy');
            document.body.removeChild(input);
        },
        closePopupOnWindowClick: function (e, id, invokerId, dotNetInstance) {
            if (!e || !e.target || !id || !invokerId || !dotNetInstance) {
                return;
            }

            let currentElement = e.target;
            while (currentElement.id !== id && currentElement.id !== invokerId) {
                currentElement = currentElement.parentNode;
                if (!currentElement) {
                    dotNetInstance.invokeMethodAsync('CloseAsync');
                    break;
                }
            }
        }
    };
}());

window.App.CodeEditor = window.App.CodeEditor || (function () {
    let _editor;
    let _overrideValue;
    let _currentLanguage;

    return {
        init: function (editorId, value, language) {
            if (!editorId) {
                return;
            }

            require.config({ paths: { 'vs': 'lib/monaco-editor/min/vs' } });
            require(['vs/editor/editor.main'], () => {
                _editor = monaco.editor.create(document.getElementById(editorId), {
                    fontSize: '16px',
                    value: _overrideValue || value || '',
                    language: language || _currentLanguage || 'razor'
                });

                _overrideValue = null;
                _currentLanguage = language || _currentLanguage;
            });
        },
        getValue: function () {
            return _editor && _editor.getValue();
        },
        setValue: function (value, language) {
            if (_editor) {
                const isValueChanging = _editor.getValue() !== value;
                if (isValueChanging) {
                    _editor.setValue(value || '');
                }

                if (language && language !== _currentLanguage) {
                    monaco.editor.setModelLanguage(_editor.getModel(), language);
                    _currentLanguage = language;
                }

                if (isValueChanging) {
                    _editor.setScrollPosition({ scrollTop: 0 });
                }
            } else {
                _overrideValue = value;
                _currentLanguage = language || _currentLanguage;
            }
        },
        setLanguage: function (language) {
            if (!_editor || _currentLanguage === language) {
                return;
            }

            monaco.editor.setModelLanguage(_editor.getModel(), language);
        },
        focus: function () {
            return _editor && _editor.focus();
        },
        resize: function () {
            _editor && _editor.layout();
        },
        dispose: function () {
            _editor = null;
            _overrideValue = null;
            _currentLanguage = null;
        }
    };
}());

window.App.Repl = window.App.Repl || (function () {
    const S_KEY_CODE = 83;

    const throttleLastTimeFuncNameMappings = {};

    let _dotNetInstance;
    let _editorContainerId;
    let _resultContainerId;
    let _originalHistoryPushStateFunction;

    function setElementHeight(elementId) {
        const element = document.getElementById(elementId);
        if (element) {
            const oldHeight = element.style.height;

            // TODO: Abstract class names
            let height =
                window.innerHeight -
                document.getElementsByClassName('repl-navbar')[0].offsetHeight;

            const heightString = `${height - 1}px`;
            element.style.height = heightString;

            return oldHeight !== heightString;
        }

        return false;
    }

    function initReplSplitter() {
        if (_editorContainerId &&
            _resultContainerId &&
            document.getElementById(_editorContainerId) &&
            document.getElementById(_resultContainerId)) {

            throttleLastTimeFuncNameMappings['resetEditor'] = new Date();
            Split(['#' + _editorContainerId, '#' + _resultContainerId], {
                elementStyle: (_, size, gutterSize) => ({
                    'width': `calc(${size}% - ${gutterSize + 1}px)`,
                }),
                gutterStyle: (_, gutterSize) => ({
                    'width': `${gutterSize}px`,
                }),
                onDrag: () => throttle(window.App.CodeEditor.resize, 30, 'resetEditor'),
                onDragEnd: window.App.CodeEditor.resize
            });
        }
    }

    function onWindowResize() {
        setElementHeight(_resultContainerId);
        setElementHeight(_editorContainerId);
        window.App.CodeEditor.resize();
    }

    function onKeyDown(e) {
        if (e.ctrlKey && e.keyCode === S_KEY_CODE) {
            e.preventDefault();

            if (_dotNetInstance && _dotNetInstance.invokeMethodAsync) {
                throttle(() => _dotNetInstance.invokeMethodAsync('TriggerCompileAsync'), 1000, 'compile');
            }
        }
    }

    function throttle(func, timeFrame, id) {
        const now = new Date();
        if (now - throttleLastTimeFuncNameMappings[id] >= timeFrame) {
            func();

            throttleLastTimeFuncNameMappings[id] = now;
        }
    }

    function enableNavigateAwayConfirmation() {
        window.onbeforeunload = () => true;

        _originalHistoryPushStateFunction = window.history.pushState;
        window.history.pushState = (originalHistoryPushStateFunction => function () {
            const newUrl = arguments[2] && arguments[2].toLowerCase();
            if (newUrl && (newUrl.endsWith('/repl') || newUrl.includes('/repl/'))) {
                return originalHistoryPushStateFunction.apply(this, arguments);
            }

            const navigateAwayConfirmed = confirm('Are you sure you want to leave REPL page? Changes you made may not be saved.');
            return navigateAwayConfirmed
                ? originalHistoryPushStateFunction.apply(this, arguments)
                : null;
        })(window.history.pushState);
    }

    function disableNavigateAwayConfirmation() {
        window.onbeforeunload = null;

        if (_originalHistoryPushStateFunction) {
            window.history.pushState = _originalHistoryPushStateFunction;
        }
    }

    return {
        init: function (editorContainerId, resultContainerId, dotNetInstance) {
            _dotNetInstance = dotNetInstance;
            _editorContainerId = editorContainerId;
            _resultContainerId = resultContainerId;

            throttleLastTimeFuncNameMappings['compile'] = new Date();

            setElementHeight(resultContainerId);
            setElementHeight(editorContainerId);

            initReplSplitter();

            window.addEventListener('resize', onWindowResize);
            window.addEventListener('keydown', onKeyDown);

            enableNavigateAwayConfirmation();
        },
        setCodeEditorContainerHeight: function (newLanguage) {
            setElementHeight(_editorContainerId);
            window.App.CodeEditor.setLanguage(newLanguage);
            window.App.CodeEditor.resize();
        },
        dispose: async function (sessionId) {
            _dotNetInstance = null;
            _editorContainerId = null;
            _resultContainerId = null;

            window.removeEventListener('resize', onWindowResize);
            window.removeEventListener('keydown', onKeyDown);

            disableNavigateAwayConfirmation();

            await window.App.CodeExecution.clearResources(sessionId);
        }
    };
}());

window.App.SaveSnippetPopup = window.App.SaveSnippetPopup || (function () {
    let _dotNetInstance;
    let _invokerId;
    let _id;

    function closePopupOnWindowClick(e) {
        window.App.closePopupOnWindowClick(e, _id, _invokerId, _dotNetInstance);
    }

    return {
        init: function (id, invokerId, dotNetInstance) {
            _dotNetInstance = dotNetInstance;
            _invokerId = invokerId;
            _id = id;

            window.addEventListener('click', closePopupOnWindowClick);
        },
        dispose: function () {
            _dotNetInstance = null;
            _invokerId = null;
            _id = null;

            window.removeEventListener('click', closePopupOnWindowClick);
        }
    };
}());

window.App.TabSettingsPopup = window.App.TabSettingsPopup || (function () {
    let _dotNetInstance;
    let _invokerId;
    let _id;

    function closePopupOnWindowClick(e) {
        window.App.closePopupOnWindowClick(e, _id, _invokerId, _dotNetInstance);
    }

    return {
        init: function (id, invokerId, dotNetInstance) {
            _dotNetInstance = dotNetInstance;
            _invokerId = invokerId;
            _id = id;

            window.addEventListener('click', closePopupOnWindowClick);
        },
        dispose: function () {
            _dotNetInstance = null;
            _invokerId = null;
            _id = null;

            window.removeEventListener('click', closePopupOnWindowClick);
        }
    };
}());

window.App.CodeExecution = window.App.CodeExecution || (function () {
    const UNEXPECTED_ERROR_MESSAGE = 'An unexpected error has occurred. Please try again later or contact the team.';
    const CACHE_NAME_PREFIX = 'blazor-repl-resources-';
    const STATIC_ASSETS_FILE_NAME = '__static-assets.json';

    let _loadedPackageDlls = null;
    let _storedPackageFiles = {};

    function jsArrayToDotNetArray(jsArray) {
        jsArray = jsArray || [];

        const dotNetArray = BINDING.mono_obj_array_new(jsArray.length);
        for (let i = 0; i < jsArray.length; ++i) {
            BINDING.mono_obj_array_set(dotNetArray, i, jsArray[i]);
        }

        return dotNetArray;
    }

    function putInCacheStorage(cache, fileName, fileBytes, contentType) {
        const cachedResponse = new Response(
            new Blob([fileBytes]),
            {
                headers: {
                    'Content-Type': contentType || 'application/octet-stream',
                    'Content-Length': fileBytes.length.toString()
                }
            });

        return cache.put(fileName, cachedResponse);
    }

    function convertBytesToBase64String(bytes) {
        let binaryString = '';
        bytes.forEach(byte => binaryString += String.fromCharCode(byte));

        return btoa(binaryString);
    }

    function convertBase64StringToBytes(base64String) {
        const binaryString = window.atob(base64String);

        const bytesCount = binaryString.length;
        const bytes = new Uint8Array(bytesCount);
        for (let i = 0; i < bytesCount; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }

        return bytes;
    }

    return {
        updateUserComponentsDll: async function (fileContent) {
            if (!fileContent) {
                return;
            }

            const fileAsBase64String = typeof fileContent === 'string' ? fileContent : BINDING.conv_string(fileContent);

            const cache = await caches.open('blazor-resources-/');

            const cacheKeys = await cache.keys();
            const userComponentsDllCacheKey = cacheKeys.find(x => x.url.indexOf('BlazorRepl.UserComponents.dll') > -1);
            if (!userComponentsDllCacheKey || !userComponentsDllCacheKey.url) {
                alert(UNEXPECTED_ERROR_MESSAGE);
                return;
            }

            const dllPath = userComponentsDllCacheKey.url.substr(window.location.origin.length);
            const dllBytes = convertBase64StringToBytes(fileAsBase64String);

            await putInCacheStorage(cache, dllPath, dllBytes);
        },
        storePackageFile: async function (rawSessionId, rawFileName, rawFileBytes) {
            if (!rawSessionId || !rawFileName || !rawFileBytes) {
                return;
            }

            const sessionId = BINDING.conv_string(rawSessionId);
            const fileName = BINDING.conv_string(rawFileName);
            const fileBytes = Blazor.platform.toUint8Array(rawFileBytes);

            _storedPackageFiles[fileName] = false;

            const cacheName = CACHE_NAME_PREFIX + sessionId;
            const cache = await caches.open(cacheName);

            await putInCacheStorage(cache, fileName, fileBytes);

            _storedPackageFiles[fileName] = true;
        },
        areAllPackageFilesStored: function () {
            const fileNames = Object.getOwnPropertyNames(_storedPackageFiles);
            if (!fileNames.length) {
                return true;
            }

            const result = fileNames.every(fileName => _storedPackageFiles[fileName]);
            if (result) {
                _storedPackageFiles = {};
            }

            return result;
        },
        loadResources: async function (rawSessionId) {
            if (!rawSessionId) {
                // Prevent endless loop on getting the loaded DLLs
                _loadedPackageDlls = jsArrayToDotNetArray([]);
                return;
            }

            const sessionId = BINDING.conv_string(rawSessionId);
            const cacheName = CACHE_NAME_PREFIX + sessionId;
            const cacheExists = await caches.has(cacheName);
            if (!cacheExists) {
                // Prevent endless loop on getting the loaded DLLs
                _loadedPackageDlls = jsArrayToDotNetArray([]);
                return;
            }

            const dlls = [];
            const scripts = [];
            const styles = [];

            const cache = await caches.open(cacheName);
            const files = await cache.keys();
            for (const file of files) {
                const response = await cache.match(file.url);
                const fileBytes = new Uint8Array(await response.arrayBuffer());
                const fileUrl = file.url.toLowerCase();

                if (fileUrl.endsWith('.js')) {
                    const fileContent = convertBytesToBase64String(fileBytes);
                    scripts.push(`data:text/javascript;base64,${fileContent}`);
                } else if (fileUrl.endsWith('.css')) {
                    const fileContent = convertBytesToBase64String(fileBytes);
                    styles.push(`data:text/css;base64,${fileContent}`);
                } else if (fileUrl.endsWith(STATIC_ASSETS_FILE_NAME)) {
                    const fileContent = new TextDecoder().decode(fileBytes);
                    const staticAssets = fileContent && JSON.parse(fileContent) || {};

                    // Place static assets as first
                    (staticAssets.scripts || []).reverse().forEach(s => scripts.unshift(s));
                    (staticAssets.styles || []).reverse().forEach(s => styles.unshift(s));
                } else {
                    // Use js_typed_array_to_array instead of jsArrayToDotNetArray so we get a byte[] instead of object[] in .NET code.
                    dlls.push(BINDING.js_typed_array_to_array(fileBytes));
                }
            }

            styles.forEach(href => {
                const link = document.createElement('link');
                link.rel = 'stylesheet';
                link.type = 'text/css';
                link.href = href;
                document.head.appendChild(link);
            });

            scripts.forEach(src => {
                const script = document.createElement('script');
                script.src = src;
                script.defer = 'defer';
                document.head.appendChild(script);
            });

            _loadedPackageDlls = jsArrayToDotNetArray(dlls);
        },
        getLoadedPackageDlls: function () {
            return _loadedPackageDlls;
        },
        updateStaticAssets: async function (sessionId, scripts, styles) {
            if (!sessionId) {
                return;
            }

            const cacheName = CACHE_NAME_PREFIX + sessionId;
            const cache = await caches.open(cacheName);

            if ((scripts && scripts.length) || (styles && styles.length)) {
                const fileBytes = new TextEncoder().encode(JSON.stringify({ scripts: scripts, styles: styles }));

                await putInCacheStorage(cache, STATIC_ASSETS_FILE_NAME, fileBytes, 'application/json');
            } else {
                await cache.delete(STATIC_ASSETS_FILE_NAME);
            }
        },
        clearResources: async function (sessionId) {
            if (!sessionId) {
                return;
            }

            const cacheName = CACHE_NAME_PREFIX + sessionId;
            await caches.delete(cacheName);
        }
    };
}());
