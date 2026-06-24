/**
 * Dynamic Form Engine - renders forms from metadata.
 */
const DynamicForm = (function () {
    let $form, formDef, recordId = null;
    let activeDefinition = null;
    let layoutMode = 'sections';

    function getFields(definition) {
        const source = definition ?? activeDefinition ?? formDef;
        return source?.Fields ?? source?.fields ?? [];
    }

    function getField(name) {
        return getFields().find(f => (f.PropertyName ?? f.propertyName) === name);
    }

    function destroyLookups($scope) {
        if (typeof MetaForgeLookups !== 'undefined') {
            MetaForgeLookups.destroyFormLookups($scope);
        }
        if (typeof MetaForgeRichText !== 'undefined') {
            MetaForgeRichText.destroyScope($scope);
        }
    }

    function resolveLayoutMode(definition, opts) {
        if (opts?.layout) return opts.layout;
        const formType = definition?.FormType ?? definition?.formType ?? '';
        return formType === 'Tabbed' ? 'tabs' : 'sections';
    }

    function applyLayoutClass($target, mode) {
        $target.toggleClass('dynamic-form--tabbed', mode === 'tabs');
    }

    function applyPreviewModeClass($target, enabled) {
        $target.toggleClass('dynamic-form-preview-mode--expanded', !!enabled);
    }

    function init(selector, definition, options) {
        const opts = options || {};
        $form = $(selector);
        formDef = definition;
        activeDefinition = definition;
        recordId = null;
        layoutMode = resolveLayoutMode(definition, opts);
        if (opts.layoutClass) {
            $form.addClass(opts.layoutClass);
        }
        applyLayoutClass($form, layoutMode);
        render();
        clearFieldErrors($form);
        bindFieldErrorClear($form);
        bindConditionalLogic($form);
        bindFileUpload($form);
        bindRichTextEditors($form);
        applyAllConditionalStates($form);
        if (opts.initLookups !== false && typeof MetaForgeLookups !== 'undefined') {
            return initLookupsForScope($form, getFields(), null).then(function () {
                applyAllConditionalStates($form);
            });
        }
        return $.when();
    }

    function renderPreview(selector, definition, options) {
        const opts = options || {};
        const $target = $(selector);
        const fields = getFields(definition);
        const previousDefinition = activeDefinition;
        const isPreviewMode = opts.previewMode !== false;

        activeDefinition = definition;
        destroyLookups($target);
        $target.empty();

        const previewLayout = resolveLayoutMode(definition, opts);
        if (opts.layoutClass) {
            $target.addClass(opts.layoutClass);
        }
        applyLayoutClass($target, previewLayout);
        applyPreviewModeClass($target, isPreviewMode && previewLayout === 'tabs');

        appendFields($target, fields, previewLayout);
        bindConditionalLogic($target);
        bindFileUpload($target);
        bindRichTextEditors($target);
        applyPreviewFieldStates($target, isPreviewMode);

        if (opts.initLookups === false || typeof MetaForgeLookups === 'undefined') {
            activeDefinition = previousDefinition;
            return $.when();
        }

        return MetaForgeLookups.initFormLookups($target, fields, opts.data || {}).then(function () {
            applyPreviewFieldStates($target, isPreviewMode);
            activeDefinition = previousDefinition;
        });
    }

    function render() {
        destroyLookups($form);
        $form.empty();
        appendFields($form, getFields());
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function escapeSelectorId(value) {
        if (typeof CSS !== 'undefined' && typeof CSS.escape === 'function') {
            return CSS.escape(value);
        }
        return String(value).replace(/([!"#$%&'()*+,./:;<=>?@[\\\]^`{|}~])/g, '\\$1');
    }

    function sectionTabId(sectionKey) {
        const slug = sectionKey === 'default' ? 'general' : sectionKey.toLowerCase().replace(/[^a-z0-9]+/g, '-');
        return `form-tab-${slug}`;
    }

    function sectionDisplayLabel(sectionKey) {
        return sectionKey === 'default' ? 'General' : sectionKey;
    }

    /** ERP-style tab metadata keyed by normalized section name. */
    const TAB_SECTION_META = {
        general: {
            icon: 'fa-circle-info',
            description: 'Primary identification, codes, and status.'
        },
        contacts: {
            icon: 'fa-address-book',
            description: 'Email, phone, and communication details.'
        },
        location: {
            icon: 'fa-location-dot',
            description: 'Country, region, and address information.'
        },
        accounting: {
            icon: 'fa-calculator',
            description: 'Credit limits, payment terms, and finance settings.'
        },
        header: {
            icon: 'fa-file-lines',
            description: 'Document header and key dates.'
        },
        shipping: {
            icon: 'fa-truck',
            description: 'Delivery address and shipment details.'
        },
        notes: {
            icon: 'fa-note-sticky',
            description: 'Comments and internal remarks.'
        },
        default: {
            icon: 'fa-layer-group',
            description: 'Core fields for this record.'
        }
    };

    function normalizeSectionKey(sectionKey) {
        if (sectionKey === 'default') return 'general';
        return String(sectionKey ?? '')
            .trim()
            .toLowerCase()
            .replace(/[^a-z0-9]+/g, '');
    }

    function getSectionMeta(sectionKey) {
        const normalized = normalizeSectionKey(sectionKey);
        return TAB_SECTION_META[normalized] ?? {
            icon: 'fa-folder-open',
            description: `Fields grouped under ${sectionDisplayLabel(sectionKey)}.`
        };
    }

    function sortFieldsByDisplayOrder(fields) {
        return [...fields].sort((a, b) =>
            (a.DisplayOrder ?? a.displayOrder ?? 0) - (b.DisplayOrder ?? b.displayOrder ?? 0));
    }

    function isFullWidthField(field) {
        const controlType = MetaForgeControlTypes.normalize(field.ControlType ?? field.controlType);
        return MetaForgeControlTypes.isFullWidth(controlType);
    }

    function appendFieldColumn($container, field) {
        const name = field.PropertyName ?? field.propertyName;
        const controlType = MetaForgeControlTypes.normalize(field.ControlType ?? field.controlType);
        const isRequired = field.IsRequired ?? field.isRequired;
        const label = field.Label ?? field.label;

        if (controlType === 'Hidden') {
            $container.append(buildControl(field));
            return;
        }

        const fullWidthClass = isFullWidthField(field) ? ' admin-form-field--full' : '';
        const col = $(`<div class="admin-form-field${fullWidthClass}" data-field-container="${name}"></div>`);
        col.append(`<label class="admin-form-label" data-field-label="${name}">${label}${isRequired ? ' <span class="required-mark">*</span>' : ''}</label>`);
        col.append(buildControl(field));
        $container.append(col);
    }

    function appendFieldsAsSections($container, fields) {
        const sections = groupBySection(fields);

        getOrderedSectionKeys(sections).forEach(sectionName => {
            if (sectionName !== 'default') {
                $container.append(`<div class="admin-form-section-title">${escapeHtml(sectionDisplayLabel(sectionName))}</div>`);
            }

            sections[sectionName].forEach(field => appendFieldColumn($container, field));
        });
    }

    function appendFieldsAsTabs($container, fields) {
        const sections = groupBySection(fields);
        const sectionKeys = getOrderedSectionKeys(sections);
        const $shell = $('<div class="dynamic-form-tabbed-shell"></div>');
        const $nav = $('<ul class="nav nav-tabs dynamic-form-tabs" role="tablist"></ul>');
        const $content = $('<div class="tab-content dynamic-form-tab-content"></div>');

        sectionKeys.forEach((sectionName, index) => {
            const tabId = sectionTabId(sectionName);
            const label = sectionDisplayLabel(sectionName);
            const meta = getSectionMeta(sectionName);
            const sectionFields = sortFieldsByDisplayOrder(sections[sectionName]);
            const fieldCount = sectionFields.length;
            const active = index === 0 ? 'active' : '';
            const selected = index === 0 ? 'true' : 'false';
            const paneState = index === 0 ? 'show active' : '';

            $nav.append(`
                <li class="nav-item" role="presentation">
                    <button class="nav-link ${active}" id="${tabId}-tab" data-bs-toggle="tab"
                        data-bs-target="#${tabId}" type="button" role="tab"
                        aria-controls="${tabId}" aria-selected="${selected}"
                        data-form-tab-key="${escapeHtml(sectionName)}">
                        <span class="dynamic-form-tab-link-inner">
                            <i class="fa-solid ${meta.icon} dynamic-form-tab-icon" aria-hidden="true"></i>
                            <span class="dynamic-form-tab-label">${escapeHtml(label)}</span>
                            <span class="dynamic-form-tab-count" title="${fieldCount} field(s)">${fieldCount}</span>
                        </span>
                    </button>
                </li>`);

            const $pane = $(`
                <div class="tab-pane fade ${paneState}" id="${tabId}" role="tabpanel"
                    aria-labelledby="${tabId}-tab" data-form-tab-pane="${escapeHtml(sectionName)}"></div>`);

            const $header = $(`
                <div class="dynamic-form-tab-pane-header">
                    <h6 class="dynamic-form-tab-pane-title">
                        <i class="fa-solid ${meta.icon}" aria-hidden="true"></i>
                        ${escapeHtml(label)}
                    </h6>
                    <p class="dynamic-form-tab-pane-desc">${escapeHtml(meta.description)}</p>
                </div>`);

            const $body = $('<div class="dynamic-form-tab-fields"></div>');
            sectionFields.forEach(field => appendFieldColumn($body, field));
            $pane.append($header).append($body);
            $content.append($pane);
        });

        $shell.append($('<div class="dynamic-form-tabs-toolbar"></div>').append($nav)).append($content);
        $container.append($shell);
        bindTabLookupLazyLoad($container);
    }

    function appendFields($container, fields, layout) {
        const mode = layout ?? layoutMode;
        const hiddenFields = [];
        const visibleFields = [];

        fields.forEach(field => {
            const controlType = MetaForgeControlTypes.normalize(field.ControlType ?? field.controlType);
            const isVisible = field.IsVisible ?? field.isVisible;
            if (controlType === 'Hidden' || isVisible === false) {
                hiddenFields.push(field);
            } else {
                visibleFields.push(field);
            }
        });

        hiddenFields.forEach(field => $container.append(buildControl(field)));

        if (mode === 'tabs') {
            appendFieldsAsTabs($container, visibleFields);
        } else {
            appendFieldsAsSections($container, visibleFields);
        }
    }

    function initLookupsForScope($scope, fields, data) {
        return MetaForgeLookups.initFormLookups($scope, fields, data || {});
    }

    function bindTabLookupLazyLoad($container) {
        $container.find('[data-bs-toggle="tab"]').off('shown.bs.tab.dynamicForm').on('shown.bs.tab.dynamicForm', function () {
            const paneSelector = $(this).attr('data-bs-target');
            const $pane = $(paneSelector);
            if (!$pane.length) return;

            $pane.find('select.lookup-autocomplete, select.lookup-select').each(function () {
                const $select = $(this);
                if ($select.hasClass('select2-hidden-accessible')) {
                    $select.trigger('change.select2');
                }
            });
        });
    }

    function buildControl(field) {
        const name = field.PropertyName ?? field.propertyName;
        const readonly = (field.IsReadOnly ?? field.isReadOnly) ? 'readonly' : '';
        const required = (field.IsRequired ?? field.isRequired) ? 'required' : '';
        const disabled = (field.IsReadOnly ?? field.isReadOnly) ? 'disabled' : '';
        const controlType = MetaForgeControlTypes.normalize(field.ControlType ?? field.controlType);
        const lookupEntity = field.LookupEntity ?? field.lookupEntity;
        const cascadeAttrs = typeof MetaForgeLookups !== 'undefined' ? MetaForgeLookups.cascadeAttrs(field) : '';
        let controlHtml;

        switch (controlType) {
            case 'TextArea':
                controlHtml = `<textarea class="form-control admin-form-control" name="${name}" ${readonly} ${required} rows="3"></textarea>`;
                break;
            case MetaForgeControlTypes.RichText: {
                const rtReadonly = (field.IsReadOnly ?? field.isReadOnly) ? 'data-readonly="true"' : '';
                controlHtml = `<div class="mf-rich-text" data-rich-text="${name}" ${rtReadonly}>
                    <input type="hidden" name="${name}" ${required} />
                    <div class="mf-rich-text-editor"></div>
                </div>`;
                break;
            }
            case 'Number':
                controlHtml = `<input type="number" step="any" class="form-control admin-form-control" name="${name}" ${readonly} ${required} />`;
                break;
            case 'Date':
                controlHtml = `<input type="date" class="form-control admin-form-control" name="${name}" ${readonly} ${required} />`;
                break;
            case 'DateTime':
                controlHtml = `<input type="datetime-local" class="form-control admin-form-control" name="${name}" ${readonly} ${required} />`;
                break;
            case 'Checkbox':
                controlHtml = `<div class="form-check mt-1"><input type="checkbox" class="form-check-input" name="${name}" ${readonly} ${disabled} /></div>`;
                break;
            case 'Dropdown':
                controlHtml = `<select class="form-select admin-form-control lookup-select" name="${name}" data-lookup="${lookupEntity || (name || '').replace(/Id$/, '')}" ${cascadeAttrs} ${required} ${disabled}></select>`;
                break;
            case 'Autocomplete':
                controlHtml = `<select class="form-select admin-form-control lookup-autocomplete" name="${name}" data-lookup="${lookupEntity || (name || '').replace(/Id$/, '')}" ${cascadeAttrs} ${required} ${disabled}></select>`;
                break;
            case 'MultiSelect':
                controlHtml = `<div class="mf-lookup-multiselect lookup-multiselect-checkboxes"
                    data-field-name="${name}"
                    data-lookup="${lookupEntity || (name || '').replace(/Ids$/, '').replace(/Id$/, '')}"
                    ${cascadeAttrs}
                    ${disabled ? 'data-disabled="true"' : ''}>
                    <button type="button" class="lookup-multiselect-toggle form-select admin-form-control text-start" aria-haspopup="listbox" aria-expanded="false" ${disabled}>
                        <span class="lookup-multiselect-summary text-muted">Select...</span>
                    </button>
                    <div class="lookup-multiselect-panel">
                        <input type="search" class="form-control form-control-sm lookup-multiselect-search" placeholder="Search..." autocomplete="off" ${disabled} />
                        <div class="lookup-multiselect-list" role="group"></div>
                        <div class="lookup-multiselect-empty small text-muted px-2 py-2 d-none">Select the parent field first.</div>
                        <div class="lookup-multiselect-loading small text-muted px-2 py-2 d-none"><i class="fa-solid fa-spinner fa-spin me-1" aria-hidden="true"></i>Loading...</div>
                        <button type="button" class="btn btn-link btn-sm lookup-multiselect-load-more d-none px-0">Load more</button>
                    </div>
                </div>`;
                break;
            case 'FileUpload':
                controlHtml = buildFileUploadControl(name, !!(field.IsReadOnly ?? field.isReadOnly));
                break;
            case 'Hidden':
                return `<input type="hidden" name="${name}" />`;
            default:
                controlHtml = `<input type="text" class="form-control admin-form-control" name="${name}" ${readonly} ${required} />`;
        }

        return wrapControlHtml(controlHtml, name);
    }

    function wrapControlHtml(controlHtml, name) {
        return `
            <div class="field-control-wrap" data-field-wrap="${name}">
                ${controlHtml}
                <div class="invalid-feedback" data-field-error="${name}"></div>
            </div>`;
    }

    const FILE_UPLOAD_ENDPOINT = '/api/metaforge/files/upload';
    const FILE_UPLOAD_MAX_BYTES = 10 * 1024 * 1024;
    const IMAGE_EXTENSIONS = ['jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp', 'svg'];

    function buildFileUploadControl(name, isReadOnly) {
        const disabled = isReadOnly ? 'disabled' : '';
        const maxMb = Math.round(FILE_UPLOAD_MAX_BYTES / (1024 * 1024));
        return `
            <div class="mf-file-upload" data-file-upload="${name}">
                <input type="hidden" name="${name}" />
                <label class="mf-file-dropzone" data-file-drop>
                    <input type="file" class="mf-file-input" ${disabled} />
                    <span class="mf-file-dropzone-inner">
                        <i class="fa-solid fa-cloud-arrow-up mf-file-dropzone-icon" aria-hidden="true"></i>
                        <span class="mf-file-dropzone-text">Drop a file here or <span class="mf-file-dropzone-browse">browse</span></span>
                        <span class="mf-file-dropzone-hint">Up to ${maxMb} MB</span>
                    </span>
                </label>
                <div class="mf-file-progress" data-file-progress hidden>
                    <div class="mf-file-progress-bar" data-file-progress-bar></div>
                </div>
                <div class="mf-file-preview" data-file-preview hidden></div>
            </div>`;
    }

    function isImageUrl(url) {
        if (!url) return false;
        const clean = String(url).split('?')[0].split('#')[0];
        const ext = clean.includes('.') ? clean.split('.').pop().toLowerCase() : '';
        return IMAGE_EXTENSIONS.includes(ext);
    }

    function fileNameFromUrl(url) {
        if (!url) return '';
        const clean = String(url).split('?')[0].split('#')[0];
        const segment = clean.substring(clean.lastIndexOf('/') + 1);
        try {
            return decodeURIComponent(segment);
        } catch (e) {
            return segment;
        }
    }

    function formatFileSize(bytes) {
        if (bytes == null || isNaN(bytes)) return '';
        if (bytes < 1024) return `${bytes} B`;
        if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
        return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    }

    function renderFilePreview($upload, info) {
        const $preview = $upload.find('[data-file-preview]');
        const url = info?.url;

        if (!url) {
            $preview.empty().attr('hidden', 'hidden');
            $upload.removeClass('mf-file-upload--has-file');
            return;
        }

        const fileName = info.fileName || fileNameFromUrl(url);
        const isImage = info.isImage != null ? !!info.isImage : isImageUrl(url);
        const sizeText = info.size ? formatFileSize(info.size) : '';
        const safeUrl = escapeHtml(url);
        const safeName = escapeHtml(fileName);

        const thumb = isImage
            ? `<a href="${safeUrl}" target="_blank" rel="noopener" class="mf-file-thumb-link">
                   <img src="${safeUrl}" alt="${safeName}" class="mf-file-thumb" /></a>`
            : `<span class="mf-file-thumb mf-file-thumb--icon"><i class="fa-solid fa-file-lines" aria-hidden="true"></i></span>`;

        $preview.html(`
            ${thumb}
            <div class="mf-file-meta">
                <a href="${safeUrl}" target="_blank" rel="noopener" class="mf-file-name" title="${safeName}">${safeName}</a>
                ${sizeText ? `<span class="mf-file-size">${escapeHtml(sizeText)}</span>` : ''}
            </div>
            <button type="button" class="mf-file-remove" data-file-remove title="Remove file" aria-label="Remove file">
                <i class="fa-solid fa-xmark" aria-hidden="true"></i>
            </button>
        `).removeAttr('hidden');

        $upload.addClass('mf-file-upload--has-file');
    }

    function setFileUploadValue($upload, info) {
        const url = info?.url ?? '';
        const $hidden = $upload.find('input[type="hidden"]').first();
        $hidden.val(url);
        renderFilePreview($upload, url ? info : null);
    }

    function showFileUploadProgress($upload, percent) {
        const $progress = $upload.find('[data-file-progress]');
        const $bar = $upload.find('[data-file-progress-bar]');
        if (percent == null) {
            $progress.attr('hidden', 'hidden');
            $bar.css('width', '0%');
            return;
        }
        $progress.removeAttr('hidden');
        $bar.css('width', `${Math.max(0, Math.min(100, percent))}%`);
    }

    function clearUploadFieldError($upload) {
        const name = $upload.attr('data-file-upload');
        if (!name) return;
        const $wrap = $upload.closest('.field-control-wrap');
        $upload.closest('.field-control-wrap, .admin-form-field').removeClass('is-invalid');
        $wrap.removeClass('is-invalid');
        $wrap.find(`[data-field-error="${name}"]`).text('').hide();
    }

    function uploadFile($upload, file) {
        if (!file) return;

        const $root = $upload.closest('form, .dynamic-form, body');

        if (file.size > FILE_UPLOAD_MAX_BYTES) {
            const name = $upload.attr('data-file-upload');
            showFieldError($root, name, `File exceeds the maximum allowed size of ${Math.round(FILE_UPLOAD_MAX_BYTES / (1024 * 1024))} MB.`);
            return;
        }

        clearUploadFieldError($upload);
        $upload.addClass('mf-file-upload--uploading');
        showFileUploadProgress($upload, 0);

        const formData = new FormData();
        formData.append('file', file);

        $.ajax({
            url: FILE_UPLOAD_ENDPOINT,
            method: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            xhr: function () {
                const xhr = new window.XMLHttpRequest();
                xhr.upload.addEventListener('progress', function (e) {
                    if (e.lengthComputable) {
                        showFileUploadProgress($upload, Math.round((e.loaded / e.total) * 100));
                    }
                });
                return xhr;
            }
        }).done(function (result) {
            setFileUploadValue($upload, {
                url: result.url ?? result.Url,
                fileName: result.fileName ?? result.FileName,
                size: result.size ?? result.Size,
                isImage: result.isImage ?? result.IsImage
            });
            applyAllConditionalStates($root);
        }).fail(function (xhr) {
            const name = $upload.attr('data-file-upload');
            const message = xhr?.responseJSON?.error ?? 'File upload failed. Please try again.';
            showFieldError($root, name, message);
        }).always(function () {
            $upload.removeClass('mf-file-upload--uploading');
            showFileUploadProgress($upload, null);
            $upload.find('.mf-file-input').val('');
        });
    }

    function bindRichTextEditors($scope) {
        if (typeof MetaForgeRichText === 'undefined') return;
        MetaForgeRichText.initScope($scope ? $($scope) : $form);
    }

    function bindFileUpload($scope) {
        const $root = $scope ? $($scope) : $form;
        if (!$root.length) return;

        $root.off('.metaforgeFileUpload');

        $root.on('change.metaforgeFileUpload', '.mf-file-input', function () {
            const file = this.files && this.files[0];
            if (file) {
                uploadFile($(this).closest('.mf-file-upload'), file);
            }
        });

        $root.on('click.metaforgeFileUpload', '[data-file-remove]', function (e) {
            e.preventDefault();
            const $upload = $(this).closest('.mf-file-upload');
            setFileUploadValue($upload, null);
            applyAllConditionalStates($root);
        });

        $root.on('dragover.metaforgeFileUpload dragenter.metaforgeFileUpload', '[data-file-drop]', function (e) {
            e.preventDefault();
            e.stopPropagation();
            $(this).closest('.mf-file-upload').addClass('mf-file-upload--dragover');
        });

        $root.on('dragleave.metaforgeFileUpload dragend.metaforgeFileUpload drop.metaforgeFileUpload', '[data-file-drop]', function () {
            $(this).closest('.mf-file-upload').removeClass('mf-file-upload--dragover');
        });

        $root.on('drop.metaforgeFileUpload', '[data-file-drop]', function (e) {
            e.preventDefault();
            e.stopPropagation();
            const $dropzone = $(this);
            if ($dropzone.find('.mf-file-input').prop('disabled')) return;
            const dt = e.originalEvent?.dataTransfer;
            const file = dt?.files && dt.files[0];
            if (file) {
                uploadFile($dropzone.closest('.mf-file-upload'), file);
            }
        });
    }

    function findFieldContainer($scope, fieldName) {
        const $byData = $scope.find(`[data-field-container="${fieldName}"]`).first();
        if ($byData.length) return $byData;

        const $wrap = findFieldWrap($scope, fieldName);
        return $wrap.closest('.admin-form-field');
    }

    function collectFormData($scope) {
        const data = {};
        $scope.find('[name]').each(function () {
            const $el = $(this);
            const name = $el.attr('name');
            data[name] = readFieldValue($el, getField(name));
        });
        collectMultiSelectData($scope, data);
        return data;
    }

    function applyPreviewFieldStates($scope, isPreviewMode) {
        if (!isPreviewMode) {
            applyAllConditionalStates($scope);
            return;
        }

        const $root = $scope ? $($scope) : $form;
        if (!$root.length) return;

        getFields().forEach(function (field) {
            const name = field.PropertyName ?? field.propertyName;
            const controlType = MetaForgeControlTypes.normalize(field.ControlType ?? field.controlType);
            if (controlType === 'Hidden') return;

            const configuredVisible = field.IsVisible ?? field.isVisible ?? true;
            const $container = findFieldContainer($root, name);
            if ($container.length) {
                $container.toggleClass('d-none', !configuredVisible);
            }
        });
    }

    function applyFieldConditionalState($scope, field) {
        if (typeof FieldConditionalEngine === 'undefined') return;

        const name = field.PropertyName ?? field.propertyName;
        const controlType = MetaForgeControlTypes.normalize(field.ControlType ?? field.controlType);
        if (controlType === 'Hidden') return;

        const data = collectFormData($scope);
        const state = FieldConditionalEngine.evaluateEffectiveState(field, data);
        const $container = findFieldContainer($scope, name);
        const $input = findFieldInput($scope, name);
        const $label = $scope.find(`[data-field-label="${name}"]`).first();

        if ($container.length) {
            $container.toggleClass('d-none', !state.visible);
        }

        if ($input.length) {
            $input.prop('required', state.required && state.visible);

            if ($input.is('select')) {
                $input.prop('disabled', false);
                const $s2 = $input.next('.select2-container');
                if (state.readOnly) {
                    $input.attr('tabindex', '-1');
                    $s2.addClass('pe-none field-readonly-select');
                } else {
                    $input.removeAttr('tabindex');
                    $s2.removeClass('pe-none field-readonly-select');
                }
            } else if ($input.hasClass('lookup-multiselect-toggle')) {
                const $multi = $input.closest('.lookup-multiselect-checkboxes');
                $multi.find('.lookup-multiselect-toggle').prop('disabled', state.readOnly);
                $multi.find('.lookup-multiselect-search').prop('disabled', state.readOnly);
                $multi.find('.lookup-multiselect-item, .lookup-multiselect-load-more').prop('disabled', state.readOnly);
                if (state.readOnly && typeof MetaForgeLookups !== 'undefined') {
                    MetaForgeLookups.closeMultiSelectPanel($multi);
                }
            } else if ($input.closest('.mf-rich-text').length) {
                if (typeof MetaForgeRichText !== 'undefined') {
                    MetaForgeRichText.setReadOnly($input.closest('.mf-rich-text'), state.readOnly);
                }
            } else if ($input.hasClass('form-check-input')) {
                $input.prop('disabled', state.readOnly);
            } else {
                $input.prop('readonly', state.readOnly);
                $input.prop('disabled', false);
            }
        }

        if ($label.length) {
            const label = field.Label ?? field.label ?? name;
            $label.html(`${label}${state.required && state.visible ? ' <span class="required-mark">*</span>' : ''}`);
        }
    }

    function applyAllConditionalStates($scope) {
        if (typeof FieldConditionalEngine === 'undefined') return;

        const $root = $scope ? $($scope) : $form;
        if (!$root.length) return;

        getFields().forEach(function (field) {
            applyFieldConditionalState($root, field);
        });
    }

    function bindConditionalLogic($scope) {
        const $root = $scope ? $($scope) : $form;
        $root.off('.conditionalLogic');
        $root.on('input.conditionalLogic change.conditionalLogic', '.form-control, .form-select, .form-check-input, .lookup-multiselect-item, .mf-rich-text input[type="hidden"]', function () {
            applyAllConditionalStates($root);
        });
    }

    function findFieldWrap($scope, fieldName) {
        return $scope.find(`[data-field-wrap="${fieldName}"]`).first();
    }

    function findFieldInput($scope, fieldName) {
        const $wrap = findFieldWrap($scope, fieldName);
        if ($wrap.length) {
            const $richText = $wrap.find('.mf-rich-text');
            if ($richText.length) {
                return $richText.find('input[type="hidden"]').first();
            }

            const $multiselect = $wrap.find('.lookup-multiselect-checkboxes');
            if ($multiselect.length) {
                return $multiselect.find('.lookup-multiselect-toggle').first();
            }

            const $input = $wrap.find('.form-control, .form-select, .form-check-input').first();
            if ($input.length) return $input;
        }

        return $scope.find(`[name="${fieldName}"]`).first();
    }

    function clearFieldErrors($scope) {
        const $root = $scope ? $($scope) : $form;
        if (!$root.length) return;

        $root.find('.is-invalid').removeClass('is-invalid');
        $root.find('.lookup-multiselect-checkboxes.is-invalid').removeClass('is-invalid');
        $root.find('.field-control-wrap.is-invalid').removeClass('is-invalid');
        $root.find('[data-field-error]').text('').hide();
        $root.find('.detail-field-wrap .is-invalid').removeClass('is-invalid');
        $root.find('.dynamic-form-tabs .nav-link').removeClass('has-validation-error');
    }

    function showFieldError($scope, fieldName, message) {
        const $root = $scope ? $($scope) : $form;
        const $wrap = findFieldWrap($root, fieldName);
        const $input = findFieldInput($root, fieldName);
        const $feedback = $wrap.find(`[data-field-error="${fieldName}"]`);

        if ($input.length) {
            $input.addClass('is-invalid');
        }
        if ($wrap.length) {
            $wrap.addClass('is-invalid');
        }
        if ($feedback.length) {
            $feedback.text(message).show();
        }

        if ($input.hasClass('lookup-select') || $input.hasClass('lookup-autocomplete')) {
            $input.next('.select2-container').find('.select2-selection').addClass('is-invalid');
        }
        if ($input.hasClass('lookup-multiselect-toggle')) {
            $input.closest('.lookup-multiselect-checkboxes').addClass('is-invalid');
        }
    }

    function markTabsWithValidationErrors($scope, fieldErrors) {
        if (layoutMode !== 'tabs') return;

        const $root = $scope ? $($scope) : $form;
        $root.find('.dynamic-form-tabs .nav-link').removeClass('has-validation-error');

        Object.keys(fieldErrors || {}).forEach(function (fieldName) {
            const $pane = findFieldInput($root, fieldName).closest('[data-form-tab-pane]');
            if (!$pane.length) return;

            const paneKey = $pane.attr('data-form-tab-pane') || 'default';
            const tabId = `${sectionTabId(paneKey)}-tab`;
            $root.find(`#${escapeSelectorId(tabId)}`).addClass('has-validation-error');
        });
    }

    function activateTabForField($scope, fieldName) {
        if (layoutMode !== 'tabs') return;

        const $root = $scope ? $($scope) : $form;
        const $pane = findFieldInput($root, fieldName).closest('.tab-pane');
        if (!$pane.length) return;

        const paneId = $pane.attr('id');
        if (!paneId) return;

        const $tabBtn = $root.find(`[data-bs-target="#${escapeSelectorId(paneId)}"]`);
        if ($tabBtn.length && typeof bootstrap !== 'undefined') {
            bootstrap.Tab.getOrCreateInstance($tabBtn[0]).show();
        }
    }

    function showFieldErrors($scope, fieldErrors) {
        clearFieldErrors($scope);
        if (!fieldErrors) return false;

        let count = 0;
        let firstFieldName = null;
        Object.keys(fieldErrors).forEach(function (fieldName) {
            const message = fieldErrors[fieldName];
            const text = Array.isArray(message) ? message[0] : message;
            if (!text) return;
            showFieldError($scope, fieldName, text);
            if (!firstFieldName) firstFieldName = fieldName;
            count++;
        });

        markTabsWithValidationErrors($scope, fieldErrors);

        if (count > 0 && firstFieldName) {
            activateTabForField($scope, firstFieldName);
            const $first = findFieldInput($scope ? $($scope) : $form, firstFieldName);
            if ($first.length) {
                $first.trigger('focus');
                $first[0]?.scrollIntoView?.({ behavior: 'smooth', block: 'center' });
            }
        }

        return count > 0;
    }

    function parseAjaxFieldErrors(xhr) {
        const json = xhr?.responseJSON;
        const fields = {};

        if (json?.fieldErrors && typeof json.fieldErrors === 'object') {
            Object.keys(json.fieldErrors).forEach(function (key) {
                const value = json.fieldErrors[key];
                fields[key] = Array.isArray(value) ? value[0] : value;
            });
        }

        return {
            general: json?.error ?? json?.title ?? xhr?.statusText ?? 'Save failed.',
            fields: fields
        };
    }

    function handleAjaxValidationError($scope, xhr) {
        const parsed = parseAjaxFieldErrors(xhr);
        if (Object.keys(parsed.fields).length > 0) {
            showFieldErrors($scope, parsed.fields);
            return true;
        }

        return false;
    }

    function validateRequiredFields($scope, fields, data) {
        clearFieldErrors($scope);
        const errors = {};
        const formData = data || collectFormData($scope ? $($scope) : $form);

        (fields || []).forEach(function (field) {
            const name = field.PropertyName ?? field.propertyName;
            const label = field.Label ?? field.label ?? name;
            const controlType = MetaForgeControlTypes.normalize(field.ControlType ?? field.controlType);

            let isRequired = field.IsRequired ?? field.isRequired;
            let isVisible = field.IsVisible ?? field.isVisible ?? true;

            if (typeof FieldConditionalEngine !== 'undefined') {
                const state = FieldConditionalEngine.evaluateEffectiveState(field, formData);
                isRequired = state.required;
                isVisible = state.visible;
            }

            if (!isRequired || !isVisible || controlType === 'Hidden') return;

            const val = formData[name];
            const isLookup = MetaForgeControlTypes.isLookup(controlType)
                || (name.endsWith('Id') && name !== 'Id');

            if (isLookup) {
                const num = parseInt(val, 10);
                if (!num || num <= 0) errors[name] = `${label} is required.`;
            } else if (controlType === 'Checkbox') {
                if (val !== true && val !== 'true' && val !== 1 && val !== '1') {
                    errors[name] = `${label} is required.`;
                }
            } else if (MetaForgeControlTypes.isRichText(controlType)) {
                const isEmpty = typeof MetaForgeRichText !== 'undefined'
                    ? MetaForgeRichText.isEmptyHtml(val)
                    : (val == null || String(val).trim() === '');
                if (isEmpty) {
                    errors[name] = `${label} is required.`;
                }
            } else if (val == null || String(val).trim() === '') {
                errors[name] = `${label} is required.`;
            }
        });

        return showFieldErrors($scope, errors) ? errors : null;
    }

    function bindFieldErrorClear($scope) {
        const $root = $scope ? $($scope) : $form;
        $root.off('.fieldValidationClear');
        $root.on('input.fieldValidationClear change.fieldValidationClear', '.form-control, .form-select, .form-check-input, .mf-rich-text input[type="hidden"]', function () {
            const name = $(this).attr('name');
            if (!name) return;

            const $wrap = findFieldWrap($root, name);
            $(this).removeClass('is-invalid');
            $wrap.removeClass('is-invalid');
            $wrap.find(`[data-field-error="${name}"]`).text('').hide();
            $(this).next('.select2-container').find('.select2-selection').removeClass('is-invalid');
        });
    }

    function groupBySection(fields) {
        const groups = {};
        fields.forEach(f => {
            const key = (f.SectionName ?? f.sectionName ?? '').trim() || 'default';
            if (!groups[key]) groups[key] = [];
            groups[key].push(f);
        });
        Object.keys(groups).forEach(key => {
            groups[key] = sortFieldsByDisplayOrder(groups[key]);
        });
        return groups;
    }

    function sectionSortOrder(fields) {
        return Math.min(...fields.map(f => f.DisplayOrder ?? f.displayOrder ?? 0));
    }

    function getOrderedSectionKeys(sections) {
        const keys = Object.keys(sections);
        keys.sort((a, b) => {
            if (a === 'default') return -1;
            if (b === 'default') return 1;
            return sectionSortOrder(sections[a]) - sectionSortOrder(sections[b]);
        });
        return keys;
    }

    function readMultiSelectValues($container) {
        if (!$container || $container.length === 0) return [];
        return $container.find('.lookup-multiselect-item:checked')
            .map(function () {
                return parseInt($(this).val(), 10);
            })
            .get()
            .filter(v => !Number.isNaN(v) && v > 0);
    }

    function collectMultiSelectData($scope, data) {
        $scope.find('.lookup-multiselect-checkboxes').each(function () {
            const name = $(this).data('fieldName');
            if (!name) return;
            data[name] = readMultiSelectValues($(this));
        });
    }

    function readFieldValue($el, field) {
        const controlType = MetaForgeControlTypes.normalize(field?.ControlType ?? field?.controlType);

        if ($el.hasClass('lookup-multiselect-checkboxes')) {
            return readMultiSelectValues($el);
        }

        if ($el.attr('type') === 'checkbox' && !$el.hasClass('lookup-multiselect-item')) {
            return $el.is(':checked');
        }

        const raw = $el.val();
        if (raw === '' || raw == null) {
            return null;
        }

        if (controlType === 'Number') {
            const num = parseFloat(raw);
            return Number.isNaN(num) ? null : num;
        }

        if (MetaForgeControlTypes.isMultiSelect(controlType)) {
            return null;
        }

        if ((MetaForgeControlTypes.isLookup(controlType)) && (field?.PropertyName ?? field?.propertyName ?? '').endsWith('Id')) {
            const num = parseInt(raw, 10);
            return Number.isNaN(num) || num === 0 ? null : num;
        }

        return raw;
    }

    function getData() {
        const data = {};
        $form.find('[name]').each(function () {
            const $el = $(this);
            const name = $el.attr('name');
            data[name] = readFieldValue($el, getField(name));
        });
        collectMultiSelectData($form, data);
        if (recordId != null && recordId !== '') {
            data.Id = parseInt(recordId, 10);
        }
        return data;
    }

    function formatDateTimeLocal(value) {
        const dt = new Date(value);
        if (isNaN(dt.getTime())) return value;

        const pad = n => String(n).padStart(2, '0');
        return `${dt.getFullYear()}-${pad(dt.getMonth() + 1)}-${pad(dt.getDate())}T${pad(dt.getHours())}:${pad(dt.getMinutes())}`;
    }

    function normalizeDataKeys(data) {
        const normalized = {};
        Object.keys(data || {}).forEach(k => {
            normalized[k] = data[k];
            normalized[k.charAt(0).toUpperCase() + k.slice(1)] = data[k];
            normalized[k.charAt(0).toLowerCase() + k.slice(1)] = data[k];
        });
        return normalized;
    }

    function getDataValue(data, name) {
        if (!data) return undefined;
        return data[name]
            ?? data[name.charAt(0).toLowerCase() + name.slice(1)]
            ?? data[name.charAt(0).toUpperCase() + name.slice(1)];
    }

    function applyScalarFieldValues(data) {
        const id = data?.Id ?? data?.id;
        if (id != null && id !== '') {
            recordId = id;
        }

        const normalized = normalizeDataKeys(data);

        $form.find('[name]').each(function () {
            const name = $(this).attr('name');
            const value = normalized[name];
            if (value === undefined || value === null) return;

            const $el = $(this);
            if ($el.is('select') || $el.closest('.lookup-multiselect-checkboxes').length) return;

            const field = getField(name);
            const controlType = MetaForgeControlTypes.normalize(field?.ControlType ?? field?.controlType);

            if ($el.attr('type') === 'checkbox') {
                $el.prop('checked', !!value);
            } else if (controlType === 'DateTime' && value) {
                $el.val(formatDateTimeLocal(value));
            } else if (controlType === 'Date' && value) {
                $el.val(MetaForgeGridDisplayFormat.formatDateInputValue(value));
            } else if (controlType === 'FileUpload') {
                $el.val(value);
                setFileUploadValue($el.closest('.mf-file-upload'), { url: value });
            } else if (MetaForgeControlTypes.isRichText(controlType)) {
                const $richText = $el.closest('.mf-rich-text');
                if ($richText.length && typeof MetaForgeRichText !== 'undefined') {
                    MetaForgeRichText.setValue($richText, value);
                } else {
                    $el.val(value);
                }
            } else {
                $el.val(value);
            }
        });
    }

    function applySelectValues(data) {
        if (!data) return;

        $form.find('select.lookup-select, select.lookup-autocomplete').each(function () {
            const name = $(this).attr('name');
            const val = getDataValue(data, name);
            if (val != null && val !== '') {
                $(this).val(String(val)).trigger('change.select2');
            }
        });
    }

    function setData(data) {
        applyScalarFieldValues(data);
        applySelectValues(data);
    }

    function setDataWhenReady(data) {
        applyScalarFieldValues(data);

        if (typeof MetaForgeLookups === 'undefined') {
            applySelectValues(data);
            applyAllConditionalStates($form);
            return $.when();
        }

        return initLookupsForScope($form, getFields(), data || {}).then(function () {
            applyAllConditionalStates($form);
        });
    }

    function load(id) {
        recordId = id;
        const entity = formDef.EntityName ?? formDef.entityName ?? $form.data('entity');
        return $.getJSON(`/api/metaforge/crud/${entity}/${id}`).then(data => setDataWhenReady(data));
    }

    function save() {
        const data = getData();
        const entity = formDef.EntityName ?? formDef.entityName ?? $form.data('entity');
        const method = recordId ? 'PUT' : 'POST';
        const url = recordId ? `/api/metaforge/crud/${entity}/${recordId}` : `/api/metaforge/crud/${entity}`;

        clearFieldErrors($form);

        return $.ajax({ url, method, contentType: 'application/json', data: JSON.stringify(data) })
            .then(result => {
                if (!recordId && (result.Id ?? result.id)) recordId = result.Id ?? result.id;
                if (typeof DynamicGrid !== 'undefined') DynamicGrid.reload();
                return result;
            })
            .fail(function (xhr) {
                handleAjaxValidationError($form, xhr);
            });
    }

    function clearFilePreviews($scope) {
        const $root = $scope ? $($scope) : $form;
        $root.find('.mf-file-upload').each(function () {
            setFileUploadValue($(this), null);
        });
    }

    function showNew() {
        recordId = null;
        clearFieldErrors($form);
        if ($form[0]?.reset) {
            $form[0].reset();
        }
        clearFilePreviews($form);
        applyAllConditionalStates($form);
        if (typeof MetaForgeLookups !== 'undefined') {
            destroyLookups($form);
            return initLookupsForScope($form, getFields(), null).then(function () {
                applyAllConditionalStates($form);
            });
        }
        return $.when();
    }

    function reset() {
        recordId = null;
        clearFieldErrors($form);
        if ($form[0]?.reset) {
            $form[0].reset();
        }
        clearFilePreviews($form);
        destroyLookups($form);
    }

    function refreshLookups(data) {
        if (typeof MetaForgeLookups !== 'undefined') {
            return MetaForgeLookups.initFormLookups($form, getFields(), data || {}).then(function () {
                applyAllConditionalStates($form);
            });
        }
        return $.when();
    }

    return {
        init,
        renderPreview,
        load,
        save,
        showNew,
        reset,
        getData,
        setData,
        setDataWhenReady,
        refreshLookups,
        clearFieldErrors,
        showFieldError,
        showFieldErrors,
        handleAjaxValidationError,
        validateRequiredFields,
        applyAllConditionalStates,
        parseAjaxFieldErrors
    };
})();
