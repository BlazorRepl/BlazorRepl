window.App = window.App || (function () {
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
        focusElement: function (selector) {
            if (!selector) {
                return;
            }

            const element = document.querySelector(selector);
            element && element.focus();
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
        if (ev.keyCode == ENTER_KEY_CODE) {
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
        // CTRL + S
        if (e.ctrlKey && e.keyCode === 83) {
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

    function base64ToArrayBuffer(base64) {
        const binaryString = window.atob(base64);
        const binaryLen = binaryString.length;
        const bytes = new Uint8Array(binaryLen);
        for (let i = 0; i < binaryLen; i++) {
            const ascii = binaryString.charCodeAt(i);
            bytes[i] = ascii;
        }

        return bytes;
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

            // TODO:
            caches.open('nuget-content/').then(function (cache) {
                if (!cache) {
                    // TODO: alert user
                    return;
                }

                const file =
                    '77u/LmJsYXpvcmVkLW1lbnUgew0KICAgIGxpc3Qtc3R5bGU6IG5vbmU7DQogICAgcGFkZGluZzogMXJlbSAwOw0KfQ0KDQogICAgLmJsYXpvcmVkLW1lbnUgbGkuaGlkZGVuIHsNCiAgICAgICAgZGlzcGxheTogbm9uZTsNCiAgICB9DQoNCiAgICAuYmxhem9yZWQtbWVudSBsaSBhIHsNCiAgICAgICAgZGlzcGxheTogYmxvY2s7DQogICAgICAgIHBhZGRpbmc6IC43NXJlbSAxcmVtOw0KICAgICAgICBjb2xvcjogIzMzMzsNCiAgICAgICAgY3Vyc29yOiBwb2ludGVyOw0KICAgIH0NCg0KICAgICAgICAuYmxhem9yZWQtbWVudSBsaSBhOmhvdmVyIHsNCiAgICAgICAgICAgIHRleHQtZGVjb3JhdGlvbjogdW5kZXJsaW5lOw0KICAgICAgICB9DQoNCiAgICAgICAgLmJsYXpvcmVkLW1lbnUgbGkgYS5hY3RpdmUgew0KICAgICAgICAgICAgY29sb3I6ICMzMzM7DQogICAgICAgIH0NCg0KICAgIC5ibGF6b3JlZC1tZW51IC5kaXNhYmxlZCB7DQogICAgICAgIGRpc3BsYXk6IGJsb2NrOw0KICAgICAgICBwYWRkaW5nOiAuNzVyZW0gMXJlbTsNCiAgICAgICAgY29sb3I6ICNkNmQ1ZDU7DQogICAgICAgIGN1cnNvcjogbm90LWFsbG93ZWQ7DQogICAgfQ0KDQouYmxhem9yZWQtc3ViLW1lbnUtaGVhZGVyIHsNCiAgICBkaXNwbGF5OiBibG9jazsNCiAgICBjb2xvcjogIzMzMzsNCiAgICBjdXJzb3I6IHBvaW50ZXI7DQp9DQoNCiAgICAuYmxhem9yZWQtc3ViLW1lbnUtaGVhZGVyIHNwYW46aG92ZXIgew0KICAgICAgICB0ZXh0LWRlY29yYXRpb246IHVuZGVybGluZTsNCiAgICB9DQoNCiAgICAuYmxhem9yZWQtc3ViLW1lbnUtaGVhZGVyIHNwYW4gew0KICAgICAgICBkaXNwbGF5OiBibG9jazsNCiAgICAgICAgcGFkZGluZzogLjc1cmVtIDFyZW07DQogICAgICAgIHBvc2l0aW9uOiByZWxhdGl2ZTsNCiAgICB9DQoNCiAgICAgICAgLmJsYXpvcmVkLXN1Yi1tZW51LWhlYWRlciBzcGFuIGkgew0KICAgICAgICAgICAgcG9zaXRpb246IGFic29sdXRlOw0KICAgICAgICAgICAgcmlnaHQ6IDA7DQogICAgICAgIH0NCg0KICAgIC5ibGF6b3JlZC1zdWItbWVudS1oZWFkZXIub3BlbiBzcGFuIHsNCiAgICAgICAgcGFkZGluZy1ib3R0b206IC43NXJlbTsNCiAgICB9DQoNCi5ibGF6b3JlZC1zdWItbWVudSB7DQogICAgZGlzcGxheTogbm9uZTsNCiAgICBwYWRkaW5nOiAwOw0KICAgIG1hcmdpbi1sZWZ0OiAxcmVtOw0KICAgIGxpc3Qtc3R5bGU6IG5vbmU7DQp9DQoNCi5ibGF6b3JlZC1zdWItbWVudS1oZWFkZXIub3BlbiAuYmxhem9yZWQtc3ViLW1lbnUgew0KICAgIGRpc3BsYXk6IGJsb2NrOw0KfQ0K';
                var arrBuffer = base64ToArrayBuffer(file);
                const response = new Response(new Blob([arrBuffer], { type: 'text/css' }));
                cache.put('blazored-menu.css', response).then(x => console.log(x));
            });
        },
        setCodeEditorContainerHeight: function () {
            if (setElementHeight(_editorContainerId, true)) {
                resetEditor();
            }
        },
        updateUserAssemblyInCacheStorage: function (file) {
            const response = new Response(new Blob([base64ToArrayBuffer(file)], { type: 'application/octet-stream' }));

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

(function () {
    caches.open('nuget-content/').then(function (cache) {
        if (!cache) {
            // TODO: alert user
            return;
        }

        cache.keys().then(function (files) {
            files.forEach(file => {
                debugger;

                file.arrayBuffer().then(arrayBuffer => {
                    var base64 = btoa(String.fromCharCode.apply(null, new Uint8Array(arrayBuffer)));
                    debugger;

                    var link = document.createElement('link');
                    link.rel = 'stylesheet';
                    link.type = 'text/css';
                    link.href = `data:text/css;base64,${base64}`;
                    document.head.appendChild(link);
                });
            });
        });
    });
}());
