(function () {
  function initTree(root) {
    if (!root || root.dataset.initialized === "true") return;
    root.dataset.initialized = "true";

    var search = root.querySelector(".org-unit-tree-search");
    var breadcrumb = root.querySelector(".org-unit-tree-breadcrumb");
    var items = Array.prototype.slice.call(root.querySelectorAll(".org-unit-tree-item"));

    root.querySelectorAll(".org-unit-tree-toggle").forEach(function (btn) {
      btn.addEventListener("click", function () {
        var item = btn.closest(".org-unit-tree-item");
        if (!item) return;
        var expanded = item.getAttribute("aria-expanded") !== "false";
        item.setAttribute("aria-expanded", expanded ? "false" : "true");
        var children = item.querySelector(":scope > .org-unit-tree-children");
        if (children) children.classList.toggle("d-none", expanded);
        var icon = btn.querySelector("i");
        if (icon) {
          icon.classList.toggle("bi-chevron-down", !expanded);
          icon.classList.toggle("bi-chevron-right", expanded);
        }
      });
    });

    function updateBreadcrumb() {
      if (!breadcrumb) return;
      var checked = root.querySelector(".org-unit-tree-input:checked");
      if (!checked) {
        breadcrumb.textContent = "";
        return;
      }
      var item = checked.closest(".org-unit-tree-item");
      breadcrumb.textContent = item ? (item.getAttribute("data-breadcrumb") || "") : "";
    }

    root.querySelectorAll(".org-unit-tree-input").forEach(function (input) {
      input.addEventListener("change", function () {
        items.forEach(function (el) {
          var inp = el.querySelector(":scope > .org-unit-tree-row .org-unit-tree-input");
          if (inp) el.setAttribute("aria-selected", inp.checked ? "true" : "false");
        });
        updateBreadcrumb();
      });
    });
    updateBreadcrumb();

    if (search) {
      search.addEventListener("input", function () {
        var q = (search.value || "").trim().toLowerCase();
        items.forEach(function (el) {
          var name = el.getAttribute("data-name") || "";
          var crumb = (el.getAttribute("data-breadcrumb") || "").toLowerCase();
          var match = !q || name.indexOf(q) >= 0 || crumb.indexOf(q) >= 0;
          el.classList.toggle("d-none", !match);
          if (match && q) {
            var parent = el.parentElement ? el.parentElement.closest(".org-unit-tree-item") : null;
            while (parent) {
              parent.classList.remove("d-none");
              parent.setAttribute("aria-expanded", "true");
              var children = parent.querySelector(":scope > .org-unit-tree-children");
              if (children) children.classList.remove("d-none");
              parent = parent.parentElement ? parent.parentElement.closest(".org-unit-tree-item") : null;
            }
          }
        });
      });
    }
  }

  function boot() {
    document.querySelectorAll(".org-unit-tree").forEach(initTree);
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", boot);
  } else {
    boot();
  }
})();
