/**
 * Shared Bootstrap UI feedback — delete confirmation modal and alerts.
 */
const MetaForgeUi = (function () {
    let deleteModal = null;
    let deleteResolve = null;

    function escapeHtml(text) {
        return String(text ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function init() {
        const modalEl = document.getElementById('confirmDeleteModal');
        if (modalEl) {
            deleteModal = bootstrap.Modal.getOrCreateInstance(modalEl);

            document.getElementById('confirmDeleteModalConfirm')?.addEventListener('click', function () {
                deleteModal.hide();
                if (deleteResolve) {
                    deleteResolve(true);
                    deleteResolve = null;
                }
            });

            modalEl.addEventListener('hidden.bs.modal', function () {
                if (deleteResolve) {
                    deleteResolve(false);
                    deleteResolve = null;
                }
            });
        }
    }

    function confirmDelete(options) {
        options = options || {};

        if (!deleteModal) {
            return Promise.resolve(window.confirm(options.message || 'Delete this item?'));
        }

        return new Promise(function (resolve) {
            deleteResolve = resolve;

            const titleEl = document.getElementById('confirmDeleteModalTitle');
            const messageEl = document.getElementById('confirmDeleteModalMessage');
            const detailEl = document.getElementById('confirmDeleteModalDetail');

            if (titleEl) {
                titleEl.innerHTML = '<i class="fa-solid fa-triangle-exclamation me-2"></i>' +
                    escapeHtml(options.title || 'Confirm Delete');
            }

            if (messageEl) {
                messageEl.textContent = options.message || 'Are you sure you want to delete this item?';
            }

            if (detailEl) {
                detailEl.textContent = options.detail || 'This action cannot be undone.';
            }

            deleteModal.show();
        });
    }

    function showAlert(message, type, autoDismissMs) {
        type = type || 'danger';
        autoDismissMs = autoDismissMs ?? 6000;

        const container = document.getElementById('appAlertContainer');
        if (!container) {
            window.alert(message);
            return;
        }

        const iconClass = type === 'success'
            ? 'fa-circle-check'
            : type === 'warning'
                ? 'fa-triangle-exclamation'
                : 'fa-circle-xmark';

        const alert = document.createElement('div');
        alert.className = 'alert alert-' + type + ' alert-dismissible fade show app-alert shadow-sm';
        alert.setAttribute('role', 'alert');
        alert.innerHTML =
            '<div class="d-flex align-items-start">' +
                '<i class="fa-solid ' + iconClass + ' me-2 mt-1"></i>' +
                '<div class="flex-grow-1">' + escapeHtml(message) + '</div>' +
                '<button type="button" class="btn-close ms-2" data-bs-dismiss="alert" aria-label="Close"></button>' +
            '</div>';

        container.appendChild(alert);

        if (autoDismissMs > 0) {
            window.setTimeout(function () {
                bootstrap.Alert.getOrCreateInstance(alert).close();
            }, autoDismissMs);
        }
    }

    return { init, confirmDelete, showAlert };
})();

$(function () {
    MetaForgeUi.init();
});
