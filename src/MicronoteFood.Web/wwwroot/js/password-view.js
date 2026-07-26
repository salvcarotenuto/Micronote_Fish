document.addEventListener("DOMContentLoaded", () => {
  document.querySelectorAll("[data-password-view]").forEach((button) => {
    const input = button
      .closest(".password-control")
      ?.querySelector("[data-password-input]");

    if (!input) {
      return;
    }

    button.addEventListener("click", (event) => {
      event.preventDefault();
      const isHidden = input.classList.toggle("password-masked");
      button.textContent = isHidden ? "Vedi" : "Nascondi";
      button.setAttribute(
        "aria-label",
        isHidden ? "Mostra password" : "Nascondi password"
      );
    });
  });
});
