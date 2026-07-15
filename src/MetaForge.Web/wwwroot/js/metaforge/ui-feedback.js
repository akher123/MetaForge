/**
 * Shared Bootstrap UI feedback — delete confirmation modal and alerts.
 */
const MetaForgeUi = (function () {
    let deleteModal = null;
    let deleteResolve = null;

    function getStrings() {
        return (window.__METAFORGE_LOCALE__ && window.__METAFORGE_LOCALE__.strings) || {};
    }

    function t(key, fallback) {
        const strings = getStrings();
        return strings[key] || fallback;
    }

    function formatTemplate(template, value) {
        if (!template || value == null) return template || '';
        return String(template).replace(/\{0\}/g, String(value));
    }

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

    function resolveConfirmVariant(options) {
        if (options.variant) {
            return options.variant;
        }

        const text = `${options.title || ''} ${options.message || ''}`.toLowerCase();
        return /delete|remove|discard/.test(text) ? 'danger' : 'warning';
    }

    function applyConfirmVariant(variant) {
        const contentEl = document.getElementById('confirmDeleteModalContent');
        const iconEl = document.getElementById('confirmDeleteModalIcon');

        if (contentEl) {
            contentEl.classList.remove('confirm-delete-content--danger', 'confirm-delete-content--warning');
            contentEl.classList.add(`confirm-delete-content--${variant}`);
        }

        if (iconEl) {
            const iconClass = variant === 'danger' ? 'fa-trash-can' : 'fa-circle-question';
            iconEl.innerHTML = `<i class="fa-solid ${iconClass}"></i>`;
        }
    }

    function confirmDelete(options) {
        options = options || {};

        if (!deleteModal) {
            return Promise.resolve(window.confirm(options.message || t('deleteThisItem', 'Delete this item?')));
        }

        return new Promise(function (resolve) {
            deleteResolve = resolve;

            const titleEl = document.getElementById('confirmDeleteModalTitle');
            const messageEl = document.getElementById('confirmDeleteModalMessage');
            const detailEl = document.getElementById('confirmDeleteModalDetail');
            const confirmBtn = document.getElementById('confirmDeleteModalConfirm');
            const cancelBtn = document.getElementById('confirmDeleteModalCancel');
            const variant = resolveConfirmVariant(options);
            const detail = options.detail ?? t('confirmDeleteDetail', 'This action cannot be undone.');
            const confirmLabel = options.confirmLabel || t('yes', 'Yes');
            const cancelLabel = options.cancelLabel || t('no', 'No');

            if (titleEl) {
                titleEl.textContent = options.title || t('confirmDeleteTitle', 'Confirm Delete');
            }

            if (messageEl) {
                messageEl.textContent = options.message || t('confirmDeleteMessage', 'Are you sure you want to delete this item?');
            }

            if (detailEl) {
                detailEl.textContent = detail;
                detailEl.classList.toggle('is-empty', !detail);
            }

            if (confirmBtn) {
                confirmBtn.setAttribute('aria-label', confirmLabel);
                confirmBtn.setAttribute('title', confirmLabel);
            }

            if (cancelBtn) {
                cancelBtn.setAttribute('aria-label', cancelLabel);
                cancelBtn.setAttribute('title', cancelLabel);
            }

            applyConfirmVariant(variant);
            deleteModal.show();
        });
    }

    /**
     * Primary label for toast copy — e.g. "Sales Order" → "Order", "Customer" → "Customer".
     */
    function entityLabelFromFormName(formName) {
        const name = String(formName ?? '').trim();
        if (!name) return t('record', 'Record');

        const words = name.split(/\s+/).filter(Boolean);
        if (words.length <= 1) return name;

        return words[words.length - 1];
    }

    function formatSavedMessage(formName) {
        return formatTemplate(t('savedSuccessfully', '{0} saved successfully.'), entityLabelFromFormName(formName));
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
        alert.className = 'alert alert-dismissible fade show app-alert mf-alert mf-alert--' + type;
        alert.setAttribute('role', 'alert');
        alert.innerHTML =
            '<div class="mf-alert-inner">' +
                '<span class="mf-alert-icon" aria-hidden="true"><i class="fa-solid ' + iconClass + '"></i></span>' +
                '<div class="mf-alert-body">' + escapeHtml(message) + '</div>' +
                '<button type="button" class="btn-close mf-alert-close" data-bs-dismiss="alert" aria-label="' + escapeHtml(t('close', 'Close')) + '"></button>' +
            '</div>';

        container.appendChild(alert);

        if (autoDismissMs > 0) {
            window.setTimeout(function () {
                bootstrap.Alert.getOrCreateInstance(alert).close();
            }, autoDismissMs);
        }
    }

    function startProgress() {
        MetaForgeProgress.start();
    }

    function doneProgress() {
        MetaForgeProgress.done();
    }

    function finishPageLoad() {
        MetaForgeProgress.finishPageLoad();
    }

    return {
        init,
        confirmDelete,
        showAlert,
        entityLabelFromFormName,
        formatSavedMessage,
        startProgress,
        doneProgress,
        finishPageLoad
    };
})();

$(function () {
    MetaForgeUi.init();
});
