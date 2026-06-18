/**
 * Quill-based rich text editor for RichText form controls.
 */
const MetaForgeRichText = (function () {
    const instances = new WeakMap();

    const DEFAULT_TOOLBAR = [
        [{ header: [1, 2, 3, false] }],
        ['bold', 'italic', 'underline', 'strike'],
        [{ color: [] }, { background: [] }],
        [{ list: 'ordered' }, { list: 'bullet' }],
        [{ align: [] }],
        ['link'],
        ['clean']
    ];

    function isEmptyHtml(html) {
        const text = String(html ?? '')
            .replace(/<[^>]*>/g, '')
            .replace(/&nbsp;/gi, ' ')
            .trim();
        return text.length === 0;
    }

    function normalizeHtml(html) {
        const value = String(html ?? '').trim();
        if (!value || value === '<p><br></p>' || value === '<p></p>') {
            return '';
        }
        return value;
    }

    function getHiddenInput($wrap) {
        return $wrap.find('input[type="hidden"]').first();
    }

    function syncHidden($wrap, quill) {
        const html = normalizeHtml(quill.root.innerHTML);
        getHiddenInput($wrap).val(html).trigger('change');
    }

    function initEditor($wrap) {
        if (!$wrap.length || instances.has($wrap[0])) {
            return instances.get($wrap[0]) ?? null;
        }

        if (typeof Quill === 'undefined') {
            return null;
        }

        const $editor = $wrap.find('.mf-rich-text-editor').first();
        if (!$editor.length) {
            return null;
        }

        const readOnly = $wrap.attr('data-readonly') === 'true';
        const quill = new Quill($editor[0], {
            theme: 'snow',
            modules: { toolbar: DEFAULT_TOOLBAR },
            readOnly
        });

        quill.on('text-change', function () {
            syncHidden($wrap, quill);
        });

        const initial = getHiddenInput($wrap).val();
        if (initial) {
            quill.root.innerHTML = initial;
        }

        instances.set($wrap[0], quill);
        $wrap.addClass('mf-rich-text--ready');
        if (readOnly) {
            $wrap.addClass('mf-rich-text--readonly');
        }

        return quill;
    }

    function destroyEditor($wrap) {
        const el = $wrap[0];
        const quill = instances.get(el);
        if (!quill) {
            return;
        }

        $wrap.find('.ql-toolbar').remove();
        $wrap.find('.mf-rich-text-editor').empty();
        $wrap.removeClass('mf-rich-text--ready mf-rich-text--readonly');
        instances.delete(el);
    }

    function initScope($scope) {
        const $root = $scope ? $($scope) : $(document);
        $root.find('[data-rich-text]').each(function () {
            initEditor($(this));
        });
    }

    function destroyScope($scope) {
        const $root = $scope ? $($scope) : $(document);
        $root.find('[data-rich-text]').each(function () {
            destroyEditor($(this));
        });
    }

    function setValue($wrap, html) {
        const $target = $($wrap);
        const normalized = normalizeHtml(html);
        getHiddenInput($target).val(normalized);

        const quill = instances.get($target[0]) ?? initEditor($target);
        if (quill) {
            quill.root.innerHTML = normalized || '';
        }
    }

    function getValue($wrap) {
        const $target = $($wrap);
        const value = getHiddenInput($target).val();
        return value == null || value === '' ? null : value;
    }

    function setReadOnly($wrap, readOnly) {
        const $target = $($wrap);
        const quill = instances.get($target[0]) ?? initEditor($target);
        if (quill) {
            quill.enable(!readOnly);
        }
        $target.attr('data-readonly', readOnly ? 'true' : 'false');
        $target.toggleClass('mf-rich-text--readonly', !!readOnly);
    }

    return {
        initScope,
        destroyScope,
        setValue,
        getValue,
        setReadOnly,
        isEmptyHtml,
        normalizeHtml
    };
})();
