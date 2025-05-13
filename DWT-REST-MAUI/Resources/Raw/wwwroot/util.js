var productkey='';
var messageType='';

// call .net
// Unified function to send messages to .NET
function sendMessageToDotNet(message) {
    try {
        message = (messageType === '') ? message : `${messageType}|${message}`;
        if (window.chrome && window.chrome.webview) {
            // For WinForms and WPF (WebView2)
            window.chrome.webview.postMessage(message);
        }
        else if (window.DotNet) {
            // For Blazor, not sure if this is the right way
            DotNet.invokeMethodAsync(blazorAppName, blazorCallbackName, message);
        }
        else if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.webwindowinterop) {
            // iOS and MacCatalyst WKWebView
            window.webkit.messageHandlers.webwindowinterop.postMessage(message);
        }
        else if (hybridWebViewHost) {
            // Android WebView
            hybridWebViewHost.sendMessage(message);
        }
        else {
            console.error("Unsupported platform or WebView environment.");
        }
    }
    catch (error) {
        console.error("Error sending message to .NET:", error);
    }
};

window.addEventListener("load", function () {
    messageType = '__RawMessage'; // hybridwebview require this
    invokeDotNet('load', 'true', '');
    messageType = ''; // reset this, after initView, we will set it as real type
});
// call .net

function invokeDotNet(context, result, error) {
    try {
        sendMessageToDotNet(JSON.stringify([context ?? '', result ?? '', error ?? '']));

    } catch (e) {
        console.error(`Error sending back: ${context}:`, e);
    }
}

async function invokeJavaScript(name, params, context) {
    let error = '';
    try {
        context = context ?? '';
        // Check if the function exists and is callable
        if (typeof window[name] === 'function') {
            // Decode the base64 string into a JSON array
            const decodedParams = JSON.parse(params);

            // Dynamically call the function with the provided parameters
            const result = await window[name](...decodedParams);

            // If a callback is provided, call it with the result
            if (context) {
                invokeDotNet(context, result??"", error);
            }
            return;
        } else {
            error = `Function ${name} is not defined or not callable.`;
            console.error(error);
            invokeDotNet(context, '', error);
        }
    } catch (e) {
        console.error(`Error invoking function ${name}:`, e);
        error = e.cause ?? JSON.stringify(e, Object.getOwnPropertyNames(e));
        invokeDotNet(context, '', error);
    }
}

function base64toBlob(base64Data, contentType = '', sliceSize = 512) {
    const byteCharacters = atob(base64Data);
    const byteArrays = [];

    for (let offset = 0; offset < byteCharacters.length; offset += sliceSize) {
        const slice = byteCharacters.slice(offset, offset + sliceSize);

        const byteNumbers = new Array(slice.length);
        for (let i = 0; i < slice.length; i++) {
            byteNumbers[i] = slice.charCodeAt(i);
        }

        const byteArray = new Uint8Array(byteNumbers);
        byteArrays.push(byteArray);
    }

    const blob = new Blob(byteArrays, { type: contentType });
    return blob;
}

async function loadSource(base64Image) {
    let doc = makesureDocOpened();
    await doc.loadSource(base64toBlob(base64Image));
}

async function loadDocument(url) {
    const response = await fetch(url, {
        method: "GET"
    });
    if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
    }
    const image = await response.arrayBuffer();
    if (editViewer) {
        let doc = makesureDocOpened();
        await doc.loadSource(new Blob([image], { type: response.headers.get("content-type") }));
    }
}

async function fileToBase64(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result.split(',')[1]); // Extract Base64 part
        reader.onerror = (error) => reject(error);
        reader.readAsDataURL(file);
    });
};

function makesureDocOpened() {
    let doc = editViewer.currentDocument;
    if (!doc) {
        doc = Dynamsoft.DDV.documentManager.createDocument();
        editViewer.openDocument(doc.uid);
    }
    return doc;
}

function setToolMode(mode) {
    editViewer.toolMode = mode;
    return (editViewer.toolMode === mode);
}

function rotateCurrentPage(angle) {
    return editViewer.rotate(angle, [editViewer.getCurrentPageIndex()]);
}

function rotateSelectedPages(angle) {
    return editViewer.rotate(angle, editViewer.thumbnail?.getSelectedPageIndices());
}

function cropCurrentPage() {
    return editViewer.crop(editViewer.getCropRect(), [editViewer.getCurrentPageIndex()]);
}

function cropSelectedPages() {
    return editViewer.crop(editViewer.getCropRect(), editViewer.thumbnail?.getSelectedPageIndices());
}

function undo() {
    return editViewer.undo();
}

function redo() {
    return editViewer.redo();
}

function setFitMode(mode) {
    editViewer.fitMode = mode;
    return (editViewer.fitMode === mode);
}

function setAnnotationMode(mode) {
    editViewer.toolMode = 'annotation';
    editViewer.annotationMode = mode;
    return (editViewer.annotationMode === mode);
}

function deleteCurrentPage() {
    return editViewer.currentDocument?.deletePages([editViewer.getCurrentPageIndex()]);
}

function deleteSelectedPages() {
    return editViewer.currentDocument?.deletePages(editViewer.thumbnail?.getSelectedPageIndices());
}

function deleteAllPages() {
    return editViewer.currentDocument?.deleteAllPages();
}

function getSelectedPagesCount() {
    return editViewer.thumbnail?.getSelectedPageIndices().length;
}

function getPageCount() {
    return editViewer.getPageCount();
}

async function saveCurrentToPng(settings) {
    if (editViewer.getPageCount() <= 0) {
        return "";
    }
    const file = await editViewer.currentDocument.saveToPng(editViewer.getCurrentPageIndex(), settings);
    return await fileToBase64(file);
}

async function saveCurrentToJpeg(settings) {
    if (editViewer.getPageCount() <= 0) {
        return "";
    }
    const file = await editViewer.currentDocument.saveToJpeg(editViewer.getCurrentPageIndex(), settings);
    return await fileToBase64(file);
}

async function saveCurrentAsTiff(settings) {
    return saveAsTiff([editViewer.getCurrentPageIndex()], settings);
}

async function saveSelectedAsTiff(settings) {
    return saveAsTiff(editViewer.thumbnail?.getSelectedPageIndices(), settings);
}

async function saveAllAsTiff(settings) {
    return saveAsTiff([...Array(editViewer.getPageCount()).keys()], settings);
}

async function saveAsTiff(indicies, settings) {
    if (editViewer.getPageCount() <= 0) {
        return "";
    }
    
    indicies = indicies ?? [...Array(editViewer.getPageCount()).keys()];
    if (indicies.length === 0) {
        return "";
    }
    if (settings) {
        const file = await editViewer.currentDocument.saveToTiff(indicies, settings);
        return await fileToBase64(file);
    }
    else {
        const file = await editViewer.currentDocument.saveToTiff(indicies);
        return await fileToBase64(file);
    }
}

async function saveCurrentAsPdf(settings) {
    return saveAsPdf([editViewer.getCurrentPageIndex()], settings);
}

async function saveAllAsPdf(settings) {
    return saveAsPdf(Array.from({ length: editViewer.getPageCount() }, (_, i) => i), settings);
}

async function saveSelectedAsPdf(settings) {
    return saveAsPdf(editViewer.thumbnail?.getSelectedPageIndices(), settings);
}

async function saveAsPdf(indicies, settings) {
    if (editViewer.getPageCount() <= 0) {
        return "";
    }

    indicies = indicies ?? [...Array(editViewer.getPageCount()).keys()];
    if (indicies.length === 0) {
        return "";
    }
    if (settings) {
        const file = await editViewer.currentDocument.saveToPdf(indicies, settings);
        return await fileToBase64(file);
    }
    else {
        const file = await editViewer.currentDocument.saveToPdf(indicies);
        return await fileToBase64(file);
    }
}

window.addEventListener('beforeunload', (event) => {
    event.preventDefault();
    event.returnValue = '';
});

function acquireImageFromCamera() {
    makesureDocOpened();
    const pcCaptureUiConfig = {
        type: Dynamsoft.DDV.Elements.Layout,
        flexDirection: "column",
        className: "ddv-capture-viewer-desktop",
        children: [
            {
                type: Dynamsoft.DDV.Elements.Layout,
                className: "ddv-capture-viewer-header-desktop",
                children: [
                    {
                        type: Dynamsoft.DDV.Elements.CameraResolution,
                        className: "ddv-capture-viewer-resolution-desktop",
                    },
                    Dynamsoft.DDV.Elements.AutoDetect,
                    {
                        type: Dynamsoft.DDV.Elements.Capture,
                        className: "ddv-capture-viewer-capture-desktop",
                    },
                    Dynamsoft.DDV.Elements.AutoCapture,
                    {
                        type: Dynamsoft.DDV.Elements.Button,
                        className: "ddv-button-close position-button-close", // Set the button's icon
                        tooltip: "close viewer", // Set tooltip for the button
                        events: {
                            click: "close", // Set the click event
                        },
                    },
                ],
            },
            Dynamsoft.DDV.Elements.MainView,
            {
                type: Dynamsoft.DDV.Elements.ImagePreview,
                className: "ddv-capture-viewer-image-preview-desktop",
            },
        ],
    };


    editViewer.hide();
    const captureViewer = new Dynamsoft.DDV.CaptureViewer({
        container: "container",
        uiConfig: pcCaptureUiConfig
    });
    captureViewer.openDocument(editViewer.currentDocument.uid); // Open a document which has pages
    captureViewer.play();
    captureViewer.on("close", () => {
        captureViewer.destroy();
        editViewer.show();
    });
}

