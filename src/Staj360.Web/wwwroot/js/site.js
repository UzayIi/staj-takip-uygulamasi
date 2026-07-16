// Mobilde sidebar aç/kapa
document.addEventListener('DOMContentLoaded', function () {
    var toggle = document.getElementById('sidebarToggle');
    var sidebar = document.getElementById('sidebar');
    if (toggle && sidebar) {
        toggle.addEventListener('click', function () {
            sidebar.classList.toggle('open');
        });
    }

    // Silme onayı: data-confirm ile işaretli formlar
    var confirmModalEl = document.getElementById('confirmModal');
    if (confirmModalEl) {
        var confirmModal = new bootstrap.Modal(confirmModalEl);
        var confirmMsgEl = document.getElementById('confirmMessage');
        var confirmBtn = document.getElementById('confirmBtn');
        var currentForm = null;

        document.querySelectorAll('form[data-confirm]').forEach(function (form) {
            form.addEventListener('submit', function (e) {
                // Sadece data-confirm-handled attribute'u yoksa modal açılır
                if (!form.hasAttribute('data-confirm-handled')) {
                    e.preventDefault();
                    currentForm = form;
                    confirmMsgEl.textContent = form.getAttribute('data-confirm');
                    confirmModal.show();
                }
            });
        });

        confirmBtn.addEventListener('click', function () {
            if (currentForm) {
                currentForm.setAttribute('data-confirm-handled', 'true');
                currentForm.submit();
            }
            confirmModal.hide();
        });
    }
