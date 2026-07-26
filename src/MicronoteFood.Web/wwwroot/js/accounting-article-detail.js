document.addEventListener("DOMContentLoaded", () => {
  const closeButton = document.querySelector("[data-accounting-article-close]");
  const close = () => {
    if (window.parent && window.parent !== window) {
      window.parent.postMessage({ type: "micronote:accounting-article:close" }, window.location.origin);
      return;
    }

    if (window.history.length > 1) {
      window.history.back();
      return;
    }

    window.close();
  };

  closeButton?.addEventListener("click", close);
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      event.preventDefault();
      close();
    }
  });
  closeButton?.focus();
});
