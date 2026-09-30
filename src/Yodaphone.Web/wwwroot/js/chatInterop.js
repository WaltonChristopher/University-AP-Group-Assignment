// Minimal JS interop used by ChatWindow.razor to keep the newest message in view.
window.chatInterop = {
    scrollToBottom: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) {
            el.scrollTop = el.scrollHeight;
        }
    }
};
