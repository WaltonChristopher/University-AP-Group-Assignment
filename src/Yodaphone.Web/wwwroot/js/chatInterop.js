// Minimal JS interop used by ChatWindow.razor to keep the newest message in view.
window.chatInterop = {
    scrollToBottom: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) {
            el.scrollTop = el.scrollHeight;
        }
    }
};
document.addEventListener("keydown", event => {
    const target = event.target;

    if (
        target instanceof HTMLTextAreaElement &&
        target.classList.contains("yp-input-bar__field") &&
        event.key === "Enter" &&
        !event.shiftKey
    ) {
        event.preventDefault();
    }
});
