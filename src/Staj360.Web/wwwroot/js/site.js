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
                if (!form.hasAttribute('data-confirm-handled')) {
                    e.preventDefault();
                    currentForm = form;
                    confirmMsgEl.textContent = form.getAttribute('data-confirm');
                    var okLabel = form.getAttribute('data-confirm-ok') || 'Evet, Sil';
                    confirmBtn.textContent = okLabel;
                    confirmModal.show();
                }
            });
        });

        confirmBtn.addEventListener('click', function () {
            if (currentForm) {
                confirmBtn.disabled = true;
                currentForm.setAttribute('data-confirm-handled', 'true');
                currentForm.submit();
            }
            confirmModal.hide();
        });

        confirmModalEl.addEventListener('hidden.bs.modal', function () {
            confirmBtn.disabled = false;
            currentForm = null;
        });
    }

    // TempData tabanlı toast (alert ile birlikte; otomatik kapanır)
    var flash = document.getElementById('flash-messages');
    var toastEl = document.getElementById('appToast');
    if (flash && toastEl && typeof bootstrap !== 'undefined') {
        var success = flash.getAttribute('data-success');
        var error = flash.getAttribute('data-error');
        var warning = flash.getAttribute('data-warning');
        var msg = success || error || warning;
        if (msg) {
            var body = document.getElementById('appToastBody');
            body.textContent = msg;
            toastEl.classList.remove('text-bg-success', 'text-bg-danger', 'text-bg-warning');
            if (success) toastEl.classList.add('text-bg-success');
            else if (error) toastEl.classList.add('text-bg-danger');
            else toastEl.classList.add('text-bg-warning');
            var toast = new bootstrap.Toast(toastEl, { delay: 4500 });
            toast.show();
        }
    }
});
