window.App = window.App || (function () {
    let _nuGetDlls = null;

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
        },
        base64ToArrayBuffer: function base64ToArrayBuffer(base64) {
            const binaryString = window.atob(base64);
            const binaryLen = binaryString.length;
            const bytes = new Uint8Array(binaryLen);
            for (let i = 0; i < binaryLen; i++) {
                const ascii = binaryString.charCodeAt(i);
                bytes[i] = ascii;
            }

            return bytes;
        },
        loadNuGetPackageFiles: async function loadNuGetPackageFiles(rawSessionId) {
            const sessionId = BINDING.conv_string(rawSessionId);
            const nuGetContentCache = await caches.open(`nuget-content-${sessionId}/`);
            if (!nuGetContentCache) {
                // TODO: alert user
                return;
            }

            const result = [];

            const files = await nuGetContentCache.keys();
            for (const file of files) {
                const response = await nuGetContentCache.match(file.url);

                if (file.url.endsWith('.css')) {
                    let fileContent = '';
                    (new Uint8Array(await response.arrayBuffer())).forEach(
                        (byte) => { fileContent += String.fromCharCode(byte) });
                    fileContent = btoa(fileContent);

                    const link = document.createElement('link');
                    link.rel = 'stylesheet';
                    link.type = 'text/css';
                    link.href = `data:text/css;base64,${fileContent}`;
                    document.head.appendChild(link);
                } else if (file.url.endsWith('.js')) {
                    let fileContent = '';
                    (new Uint8Array(await response.arrayBuffer())).forEach(
                        (byte) => { fileContent += String.fromCharCode(byte) });
                    fileContent = btoa(fileContent);

                    const link = document.createElement('script');
                    link.src = `data:text/javascript;base64,${fileContent}`;
                    document.body.appendChild(link);
                } else {
                    const rawFileBytes = new Uint8Array(await response.arrayBuffer());

                    //const fileBytes = BINDING.mono_obj_array_new();
                    //BINDING.mono_obj_array_set(rawFileBytes)

                    //console.log(BINDING.js_typed_array_to_array(rawFileBytes));

                    result.push(rawFileBytes);
                }
            }

            console.log(result.map(x => x.length).join());
            //_nuGetDlls = BINDING.js_typed_array_to_array(result);
            return;

            console.log(Blazor.platform);
            console.log(BINDING);

            debugger;

            // IEnum<byte[]> (.NET) -> Uint8Array[] (JS)
            _nuGetDlls = result;
        },
        getNuGetDlls: () => _nuGetDlls
    };
}());

window.App.CodeEditor = window.App.CodeEditor || (function () {
    let _editor;
    let _overrideValue;

    function initEditor(editorId, value) {
        if (!editorId) {
            return;
        }

        require.config({ paths: { 'vs': 'lib/monaco-editor/min/vs' } });
        require(['vs/editor/editor.main'], () => {
            _editor = monaco.editor.create(document.getElementById(editorId), {
                fontSize: '16px',
                value: _overrideValue || value || '',
                language: 'razor'
            });

            _overrideValue = null;
        });
    }

    function getValue() {
        return _editor && _editor.getValue();
    }

    function setValue(value) {
        if (_editor) {
            _editor.setValue(value || '');
        } else {
            _overrideValue = value;
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

window.App.TabManager = window.App.TabManager || (function () {
    const ENTER_KEY_CODE = 13;

    let _dotNetInstance;
    let _newTabInput;

    function onNewTabInputKeyDown(ev) {
        if (ev.keyCode === ENTER_KEY_CODE) {
            ev.preventDefault();

            if (_dotNetInstance && _dotNetInstance.invokeMethodAsync) {
                _dotNetInstance.invokeMethodAsync('CreateTabAsync');
            }
        }
    }

    return {
        init: function (newTabInputSelector, dotNetInstance) {
            _dotNetInstance = dotNetInstance;
            _newTabInput = document.querySelector(newTabInputSelector);
            if (_newTabInput) {
                _newTabInput.addEventListener('keydown', onNewTabInputKeyDown);
            }
        },
        dispose: function () {
            _dotNetInstance = null;

            if (_newTabInput) {
                _newTabInput.removeEventListener('keydown', onNewTabInputKeyDown);
            }
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

    function resetEditor() {
        const value = window.App.CodeEditor.getValue();
        const oldEditorElement = document.getElementById(_editorId);
        if (oldEditorElement && oldEditorElement.childNodes) {
            oldEditorElement.childNodes.forEach(c => oldEditorElement.removeChild(c));
        }

        window.App.CodeEditor.initEditor(_editorId, value);
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

            enableNavigateAwayConfirmation();
        },
        setCodeEditorContainerHeight: function () {
            if (setElementHeight(_editorContainerId, true)) {
                resetEditor();
            }
        },
        updateUserAssemblyInCacheStorage: function (file) {
            const response = new Response(new Blob([window.App.base64ToArrayBuffer(file)], { type: 'application/octet-stream' }));

            caches.open('blazor-resources-/').then(function (cache) {
                if (!cache) {
                    // TODO: alert user
                    return;
                }

                cache.keys().then(function (keys) {
                    const keysForDelete = keys.filter(x => x.url.indexOf('UserComponents') > -1);

                    const dll = keysForDelete.find(x => x.url.indexOf('dll') > -1).url.substr(window.location.origin.length);
                    cache.delete(dll).then(function () {
                        cache.put(dll, response).then(function () { });
                    });
                });
            });
        },
        dispose: function () {
            _dotNetInstance = null;
            _editorContainerId = null;
            _resultContainerId = null;
            _editorId = null;

            window.removeEventListener('resize', onWindowResize);
            window.removeEventListener('keydown', onKeyDown);

            disableNavigateAwayConfirmation();
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

window.App.NugetPackageInstallerPopup = window.App.NugetPackageInstallerPopup || (function () {
    let _dotNetInstance;
    let _sessionId;

    return {
        init: function (dotNetInstance) {
            _dotNetInstance = dotNetInstance;
            _sessionId = new Date().getTime();
            return _sessionId.toString();
        },
        addPackageFilesToCache: async function (rawFileName, rawFileBytes) {
            if (!rawFileName || !rawFileBytes) {
                return;
            }

            const nuGetContentCache = await caches.open(`nuget-content-${_sessionId}/`);
            if (!nuGetContentCache) {
                return;
            }

            const fileName = BINDING.conv_string(rawFileName);
            const fileBytes = Blazor.platform.toUint8Array(rawFileBytes);

            const cachedResponse = new Response(
                new Blob([fileBytes]),
                {
                    headers: {
                        'Content-Type': 'application/octet-stream',
                        'Content-Length': fileBytes.length.toString(),
                    }
                });

            await nuGetContentCache.put(fileName, cachedResponse);
        },
        dispose: function () {
            _dotNetInstance = null;
            _sessionId = null;
        }
    };
}());
