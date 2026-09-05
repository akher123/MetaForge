(function () {
    'use strict';

    function initPasswordToggles() {
        document.querySelectorAll('.auth-password-toggle').forEach(function (toggle) {
            var wrap = toggle.closest('.auth-input-wrap');
            if (!wrap) return;

            var input = wrap.querySelector('.auth-input--password, input[type="password"]');
            if (!input) return;

            toggle.addEventListener('click', function () {
                var show = input.type === 'password';
                input.type = show ? 'text' : 'password';
                toggle.setAttribute('aria-pressed', show ? 'true' : 'false');
                toggle.setAttribute(
                    'aria-label',
                    show ? toggle.getAttribute('data-label-hide') : toggle.getAttribute('data-label-show')
                );
                var icon = toggle.querySelector('i');
                if (icon) {
                    icon.className = show ? 'fa-solid fa-eye-slash' : 'fa-solid fa-eye';
                }
            });
        });
    }

    function syncInvalidInputState(input) {
        var isInvalid = input.classList.contains('input-validation-error')
            || input.getAttribute('aria-invalid') === 'true';
        input.classList.toggle('is-invalid', isInvalid);
    }

    function initValidationStyles() {
        document.querySelectorAll('.auth-form .auth-input').forEach(function (input) {
            syncInvalidInputState(input);
            input.addEventListener('blur', function () { syncInvalidInputState(input); });
            input.addEventListener('input', function () { syncInvalidInputState(input); });
        });

        if (typeof jQuery !== 'undefined' && jQuery.validator) {
            jQuery(document).on('focusout keyup', '.auth-form .auth-input', function () {
                syncInvalidInputState(this);
            });
        }
    }

    function initSubmitLoading() {
        var form = document.querySelector('.auth-form[data-signing-in]');
        if (!form) return;

        var submitBtn = document.getElementById('authSubmitBtn');
        if (!submitBtn) return;

        var signingInText = form.getAttribute('data-signing-in') || 'Signing in…';
        var labelEl = submitBtn.querySelector('.auth-submit-label');
        var iconEl = submitBtn.querySelector('.auth-submit-icon');
        var defaultLabel = labelEl ? labelEl.textContent : submitBtn.textContent;

        form.addEventListener('submit', function () {
            if (typeof jQuery !== 'undefined' && jQuery.fn.validate) {
                var validator = jQuery(form).data('validator');
                if (validator && !validator.form()) return;
            }

            submitBtn.disabled = true;
            submitBtn.classList.add('is-loading');
            submitBtn.setAttribute('aria-busy', 'true');

            if (labelEl) {
                labelEl.textContent = signingInText;
            }

            if (iconEl) {
                iconEl.className = 'fa-solid fa-circle-notch fa-spin auth-submit-icon ms-2';
            }
        });
    }

    function init() {
        initPasswordToggles();
        initValidationStyles();
        initSubmitLoading();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
