export function init(wrapper, element, inputFile) {


    // Add a class when the user drags a file over the drop zone
    function onDragHover(e) {
        e.preventDefault();
        element.classList.add("hover");
    }

    function onDragLeave(e) {
        e.preventDefault();
        element.classList.remove("hover");
    }

    // Handle the paste and drop events
    function onDrop(e) {
        e.preventDefault();
        element.classList.remove("hover");

        // Set the files property of the input element and raise the change event
        inputFile.files = e.dataTransfer.files;
        const event = new Event('change', { bubbles: true });
        inputFile.dispatchEvent(event);
    }

    function onPaste(e) {
        // Set the files property of the input element and raise the change event
        inputFile.files = e.clipboardData.files;
        const event = new Event('change', { bubbles: true });
        inputFile.dispatchEvent(event);
    }

    // Register all events
    element.addEventListener("dragenter", onDragHover);
    element.addEventListener("dragover", onDragHover);
    element.addEventListener("dragleave", onDragLeave);
    element.addEventListener("drop", onDrop);
    element.addEventListener('paste', onPaste);

    // The returned object allows to unregister the events when the Blazor component is destroyed
    return {
        dispose: () => {
            element.removeEventListener('dragenter', onDragHover);
            element.removeEventListener('dragover', onDragHover);
            element.removeEventListener('dragleave', onDragLeave);
            element.removeEventListener("drop", onDrop);
            element.removeEventListener('paste', onPaste);
        }
    }
}
