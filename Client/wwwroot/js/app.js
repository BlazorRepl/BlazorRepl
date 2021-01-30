window.App = window.App || (function () {
    return {
        reloadIFrame: function (id, newSrc) {
            const iFrame = document.getElementById(id);
            if (iFrame) {
                if (newSrc && iFrame.src !== `${window.location.origin}${newSrc}`) {
                    iFrame.src = newSrc;
                } else {
                    iFrame.contentWindow.location.reload();
                }
            }
        },
        changeDisplayUrl: function (url) {
            if (!url) {
                return;
            }

            window.history.pushState(null, null, url);
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
        }
    };
}());

window.App.CodeEditor = window.App.CodeEditor || (function () {
    let _editor;
    let _overrideValue;
    let _currentLanguage;

    function initEditor(editorId, value, language) {
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
    }

    function getValue() {
        return _editor && _editor.getValue();
    }

    function setValue(value, language) {
        if (_editor) {
            _editor.setValue(value || '');
            if (language && language !== _currentLanguage) {
                monaco.editor.setModelLanguage(_editor.getModel(), language);
                _currentLanguage = language;
            }
        } else {
            _overrideValue = value;
            _currentLanguage = language || _currentLanguage;
        }
    }

    function focus() {
        return _editor && _editor.focus();
    }

    return {
        init: initEditor,
        initEditor: initEditor,
        getValue: getValue,
        setValue: setValue,
        focus: focus,
        dispose: function () {
            _editor = null;
        }
    };
}());

window.App.Repl = window.App.Repl || (function () {
    const S_KEY_CODE = 83;

    const throttleLastTimeFuncNameMappings = {};

    let _dotNetInstance;
    let _editorContainerId;
    let _resultContainerId;
    let _editorId;
    let _originalHistoryPushStateFunction;

    function setElementHeight(elementId, excludeTabsHeight) {
        const element = document.getElementById(elementId);
        if (element) {
            const oldHeight = element.style.height;

            // TODO: Abstract class names
            let height =
                window.innerHeight -
                document.getElementsByClassName('repl-navbar')[0].offsetHeight;

            if (excludeTabsHeight) {
                height -= document.getElementsByClassName('tabs-wrapper')[0].offsetHeight;
            }

            const heightString = `${height}px`;
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
                elementStyle: (dimension, size, gutterSize) => ({
                    'flex-basis': `calc(${size}% - ${gutterSize + 1}px)`,
                }),
                gutterStyle: (dimension, gutterSize) => ({
                    'flex-basis': `${gutterSize}px`,
                }),
                onDrag: () => throttle(resetEditor, 100, 'resetEditor'),
                onDragEnd: resetEditor
            });
        }
    }

    function resetEditor(newLanguage) {
        const value = window.App.CodeEditor.getValue();
        const oldEditorElement = document.getElementById(_editorId);
        if (oldEditorElement && oldEditorElement.childNodes) {
            oldEditorElement.childNodes.forEach(c => oldEditorElement.removeChild(c));
        }

        window.App.CodeEditor.initEditor(_editorId, value, newLanguage);
    }

    function onWindowResize() {
        setElementHeight(_resultContainerId);
        setElementHeight(_editorContainerId, true);
        resetEditor();
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
        init: function (editorContainerId, resultContainerId, editorId, dotNetInstance) {
            _dotNetInstance = dotNetInstance;
            _editorContainerId = editorContainerId;
            _resultContainerId = resultContainerId;
            _editorId = editorId;

            throttleLastTimeFuncNameMappings['compile'] = new Date();

            setElementHeight(resultContainerId);
            setElementHeight(editorContainerId, true);

            initReplSplitter();

            window.addEventListener('resize', onWindowResize);
            window.addEventListener('keydown', onKeyDown);

            //enableNavigateAwayConfirmation();
        },
        setCodeEditorContainerHeight: function (newLanguage) {
            setElementHeight(_editorContainerId, true);
            resetEditor(newLanguage);
        },
        dispose: async function (sessionId) {
            _dotNetInstance = null;
            _editorContainerId = null;
            _resultContainerId = null;
            _editorId = null;

            window.removeEventListener('resize', onWindowResize);
            window.removeEventListener('keydown', onKeyDown);

            disableNavigateAwayConfirmation();

            await window.App.CodeExecution.clearPackages(sessionId);
        }
    };
}());

window.App.SaveSnippetPopup = window.App.SaveSnippetPopup || (function () {
    let _dotNetInstance;
    let _invokerId;
    let _id;

    function closePopupOnWindowClick(e) {
        if (!_dotNetInstance || !_invokerId || !_id) {
            return;
        }

        let currentElement = e.target;
        while (currentElement.id !== _id && currentElement.id !== _invokerId) {
            currentElement = currentElement.parentNode;
            if (!currentElement) {
                _dotNetInstance.invokeMethodAsync('CloseAsync');
                break;
            }
        }
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

    let _loadedPackageDlls = null;

    function jsArrayToDotNetArray(jsArray) {
        jsArray = jsArray || [];

        const dotNetArray = BINDING.mono_obj_array_new(jsArray.length);
        for (let i = 0; i < jsArray.length; ++i) {
            BINDING.mono_obj_array_set(dotNetArray, i, jsArray[i]);
        }

        return dotNetArray;
    }

    async function putInCacheStorage(cache, fileName, fileBytes) {
        const cachedResponse = new Response(
            new Blob([fileBytes]),
            {
                headers: {
                    'Content-Type': 'application/octet-stream',
                    'Content-Length': fileBytes.length.toString()
                }
            });

        await cache.put(fileName, cachedResponse);
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
        updateUserComponentsDll: async function (fileAsBase64String) {
            if (!fileAsBase64String) {
                return;
            }

            const cache = await caches.open('blazor-resources-/');
            if (!cache) {
                alert(UNEXPECTED_ERROR_MESSAGE);
                return;
            }

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
            const packagesCache = await caches.open(`packages-${sessionId}/`);
            if (!packagesCache) {
                return;
            }

            const fileName = BINDING.conv_string(rawFileName);
            const fileBytes = Blazor.platform.toUint8Array(rawFileBytes);
            await putInCacheStorage(packagesCache, fileName, fileBytes);
        },
        loadPackageFiles: async function (rawSessionId) {
            if (!rawSessionId) {
                return;
            }

            const sessionId = BINDING.conv_string(rawSessionId);
            const packagesCache = await caches.open(`packages-${sessionId}/`);
            if (!packagesCache) {
                return;
            }

            const dlls = [];

            const files = await packagesCache.keys();
            for (const file of files) {
                const response = await packagesCache.match(file.url);
                const bytes = new Uint8Array(await response.arrayBuffer());

                if (file.url.endsWith('.css')) {
                    const fileContent = convertBytesToBase64String(bytes);
                    const link = document.createElement('link');
                    link.rel = 'stylesheet';
                    link.type = 'text/css';
                    link.href = `data:text/css;base64,${fileContent}`;
                    document.head.appendChild(link);
                } else if (file.url.endsWith('.js')) {
                    const fileContent = convertBytesToBase64String(bytes);
                    const script = document.createElement('script');
                    script.src = `data:text/javascript;base64,${fileContent}`;
                    document.body.appendChild(script);
                } else {
                    // Use js_typed_array_to_array instead of jsArrayToDotNetArray so we get a byte[] instead of object[] in .NET code.
                    dlls.push(BINDING.js_typed_array_to_array(bytes));
                }
            }

            _loadedPackageDlls = jsArrayToDotNetArray(dlls);
        },
        getLoadedPackageDlls: function () {
            return _loadedPackageDlls;
        },
        clearPackages: async function (sessionId) {
            if (!sessionId) {
                return;
            }

            await caches.delete(`packages-${sessionId}/`);
        }
    };
}());
