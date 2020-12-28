﻿window.App = window.App || (function () {
    return {
        reloadIFrame: function (id, newSrc) {
            const iFrame = document.getElementById(id);
            if (iFrame) {
                if (newSrc) {
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
        getAvailableFiles: async function getAvailableFiles(timestamp) {
            var result = [];

            await caches.open(`nuget-content-${timestamp}/`).then(async function (cache) {
                if (!cache) {
                    // TODO: alert user
                    return;
                }

                var files = await cache.keys();
                for await (const file of files) {
                    const response = await cache.match(file.url);

                    let fileContent = '';
                    (new Uint8Array(await response.arrayBuffer())).forEach(
                        (byte) => { fileContent += String.fromCharCode(byte) }
                    )
                    fileContent = btoa(fileContent);

                    if (file.url.endsWith(".css")) {
                        const link = document.createElement('link');
                        link.rel = 'stylesheet';
                        link.type = 'text/css';
                        link.href = `data:text/css;base64,${fileContent}`;
                        document.head.appendChild(link);
                    } else if (file.url.endsWith('.js')) {
                        const link = document.createElement('script');
                        link.src = `data:text/javascript;base64,${fileContent}`;
                        document.body.appendChild(link);
                    } 
                    else {
                        result.push(fileContent);
                    }
                }
            });

            return result;
        }
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
        addPackageFilesToCache: function (packageFiles) {
            caches.open(`nuget-content-${_sessionId}/`).then(function (cache) {
                if (!cache) {
                    // TODO: do we need that?
                    return;
                }

                Object.getOwnPropertyNames(packageFiles).forEach(fileName => {
                    var fileBase64 = packageFiles[fileName];
                    var arrBuffer = window.App.base64ToArrayBuffer(fileBase64);
                    const response = new Response(new Blob([arrBuffer]));
                    cache.put(fileName, response).then(x => console.log(x));
                })
            });
        },
        dispose: function () {
            _dotNetInstance = null;
            _sessionId = null
        }
    };
}());
