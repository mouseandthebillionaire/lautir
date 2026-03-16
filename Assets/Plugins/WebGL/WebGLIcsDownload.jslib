mergeInto(LibraryManager.library, {
    WebGLDownloadIcs__sig: 'vii',
    WebGLDownloadIcs__proxy: 'sync',
    WebGLDownloadIcs: function (filenamePtr, contentPtr) {
        var filename = UTF8ToString(filenamePtr);
        var content = UTF8ToString(contentPtr);

        try {
            var blob = new Blob([content], { type: "text/calendar;charset=utf-8" });
            var url = URL.createObjectURL(blob);
            var a = document.createElement("a");
            a.href = url;
            a.download = filename || "reminder.ics";
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
        } catch (e) {
            console.error("WebGLDownloadIcs failed", e, content);
        }
    }
});

