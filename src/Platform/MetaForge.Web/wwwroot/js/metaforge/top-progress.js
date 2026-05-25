/**
 * Thin top progress bar for page loads, navigation, and AJAX activity.
 */
const MetaForgeProgress = (function () {
    let barEl = null;
    let activeCount = 0;
    let pageLoadActive = false;
    let progress = 0;
    let trickleTimer = null;
    let hideTimer = null;
    let isVisible = false;

    function finishPageLoad() {
        pageLoadActive = false;
        tryHide();
    }

    function init() {
        barEl = document.getElementById('topProgressBar');
        if (!barEl) return;

        pageLoadActive = true;
        show();

        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', finishPageLoad);
        } else {
            finishPageLoad();
        }

        window.addEventListener('load', finishPageLoad);

        window.addEventListener('pageshow', function (event) {
            if (event.persisted) {
                finishPageLoad();
            }
        });

        document.addEventListener('click', onDocumentClick, true);
        document.addEventListener('submit', onFormSubmit, true);

        if (window.jQuery) {
            jQuery(document)
                .on('ajaxSend.metaforgeProgress', function (_event, _xhr, settings) {
                    if (!shouldTrackAjax(settings)) return;
                    start();
                })
                .on('ajaxComplete.metaforgeProgress', function (_event, _xhr, settings) {
                    if (!shouldTrackAjax(settings)) return;
                    done();
                })
                .on('ajaxStop.metaforgeProgress', function () {
                    if (!pageLoadActive && activeCount === 0) {
                        tryHide();
                    }
                });
        }
    }

    function shouldTrackAjax(settings) {
        if (!settings) return true;
        if (settings.global === false) return false;
        if (settings.metaforgeProgress === false) return false;

        const url = settings.url || '';
        if (/\/api\/metaforge\/grid\//i.test(url)) return false;
        if (/\/api\/metaforge\/lookups\//i.test(url)) return false;

        return true;
    }

    function onDocumentClick(event) {
        const anchor = event.target.closest('a[href]');
        if (!shouldTrackNavigation(anchor, event)) return;
        start();
    }

    function onFormSubmit(event) {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return;
        if (form.dataset.noProgress !== undefined) return;
        if (form.dataset.ajax === 'true') return;
        start();
    }

    function shouldTrackNavigation(anchor, event) {
        if (!anchor) return false;
        if (event.defaultPrevented) return false;
        if (event.button !== 0) return false;
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return false;
        if (anchor.target === '_blank') return false;
        if (anchor.hasAttribute('download')) return false;
        if (anchor.dataset.noProgress !== undefined) return false;
        if (anchor.getAttribute('data-bs-toggle')) return false;
        if (anchor.getAttribute('role') === 'button') return false;

        const href = anchor.getAttribute('href');
        if (!href || href === '#' || href.startsWith('#') || href.startsWith('javascript:')) return false;

        try {
            const url = new URL(anchor.href, window.location.href);
            if (url.origin !== window.location.origin) return false;
            if (/\/api\/metaforge\/grid\//i.test(url.pathname)) return false;
            if (
                url.pathname === window.location.pathname &&
                url.search === window.location.search &&
                url.hash
            ) {
                return false;
            }
        } catch (_) {
            return false;
        }

        return true;
    }

    function setProgress(value) {
        progress = Math.max(0, Math.min(1, value));
        if (!barEl) return;

        barEl.style.transform = 'scaleX(' + progress + ')';
        barEl.parentElement?.setAttribute('aria-valuenow', String(Math.round(progress * 100)));
    }

    function startTrickle() {
        clearInterval(trickleTimer);
        trickleTimer = window.setInterval(function () {
            if (progress >= 0.92) return;
            setProgress(progress + (1 - progress) * 0.08);
        }, 180);
    }

    function show() {
        if (!barEl || isVisible) return;

        isVisible = true;
        clearTimeout(hideTimer);
        clearInterval(trickleTimer);

        const container = barEl.parentElement;
        container?.classList.remove('is-complete');
        container?.classList.add('is-active');
        container?.setAttribute('aria-hidden', 'false');

        progress = 0;
        setProgress(0.08);
        startTrickle();
    }

    function tryHide() {
        if (pageLoadActive || activeCount > 0) return;

        clearInterval(trickleTimer);
        clearTimeout(hideTimer);

        const container = barEl.parentElement;
        container?.classList.remove('is-active');
        container?.classList.add('is-complete');
        setProgress(1);

        hideTimer = window.setTimeout(function () {
            container?.classList.remove('is-complete');
            container?.setAttribute('aria-hidden', 'true');
            setProgress(0);
            isVisible = false;
        }, 280);
    }

    function start() {
        activeCount += 1;
        show();
    }

    function done() {
        activeCount = Math.max(0, activeCount - 1);
        tryHide();
    }

    function reset() {
        pageLoadActive = false;
        activeCount = 0;
        tryHide();
    }

    return { init, start, done, reset, finishPageLoad };
})();

MetaForgeProgress.init();
