/**
 * Visual validation rule builder for Form Builder field rows.
 */
const ValidationRuleBuilder = (function () {
    let catalog = [];
    let modalInstance = null;
    let activeRow = null;
    let activeRules = [];

    function init() {
        loadCatalog();
        ensureModal();
        bindGlobalEvents();
    }

    function loadCatalog() {
        $.getJSON('/api/metaforge/formconfig/validation-rules')
            .done(function (data) {
                catalog = data || [];
                populateRuleTypeSelect();
            })
            .fail(function () {
                catalog = getFallbackCatalog();
                populateRuleTypeSelect();
            });
    }

    function getFallbackCatalog() {
        return [
            { type: 'maxLength', label: 'Maximum Length', category: 'Text', parameters: [{ name: 'value', label: 'Max characters', inputType: 'number', required: true }] },
            { type: 'minLength', label: 'Minimum Length', category: 'Text', parameters: [{ name: 'value', label: 'Min characters', inputType: 'number', required: true }] },
            { type: 'range', label: 'Numeric Range', category: 'Number', parameters: [{ name: 'min', label: 'Minimum', inputType: 'number', required: true }, { name: 'max', label: 'Maximum', inputType: 'number', required: true }] },
            { type: 'email', label: 'Email Address', category: 'Format', parameters: [] },
            { type: 'phone', label: 'Phone Number', category: 'Format', parameters: [] },
            { type: 'url', label: 'URL / Website', category: 'Format', parameters: [] },
            { type: 'regex', label: 'Custom Pattern (Regex)', category: 'Advanced', parameters: [{ name: 'value', label: 'Regular expression', inputType: 'text', required: true }] },
            { type: 'compareField', label: 'Compare to Another Field', category: 'Cross-field', parameters: [{ name: 'otherField', label: 'Other field', inputType: 'text', required: true }, { name: 'operator', label: 'Comparison', inputType: 'select', required: true, options: [{ value: 'gte', label: 'Greater than or equal' }, { value: 'gt', label: 'Greater than' }, { value: 'lte', label: 'Less than or equal' }, { value: 'lt', label: 'Less than' }, { value: 'equal', label: 'Equal to' }, { value: 'notEqual', label: 'Not equal to' }] }] },
            { type: 'unique', label: 'Must Be Unique', category: 'Data Integrity', parameters: [{ name: 'columns', label: 'Columns (comma-separated, optional)', inputType: 'text', required: false, placeholder: 'Code' }] }
        ];
    }

    function ensureModal() {
        if ($('#validationRuleModal').length)
            return;

        $('body').append(`
            <div class="modal fade" id="validationRuleModal" tabindex="-1" aria-labelledby="validationRuleModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-lg modal-dialog-scrollable">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="validationRuleModalLabel">
                                <i class="fa-solid fa-shield-halved me-2"></i>Validation Rules
                            </h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <div class="validation-rule-field-context mb-3">
                                Field: <strong id="validationRuleFieldName"></strong>
                            </div>
                            <div id="validationRuleList" class="validation-rule-list mb-3"></div>
                            <div class="validation-rule-add card admin-card">
                                <div class="card-header py-2"><i class="fa-solid fa-plus me-1"></i>Add Rule</div>
                                <div class="card-body">
                                    <div class="row g-2 align-items-end">
                                        <div class="col-md-5">
                                            <label class="form-label" for="validationRuleTypeSelect">Rule type</label>
                                            <select id="validationRuleTypeSelect" class="form-select form-select-sm"></select>
                                        </div>
                                        <div class="col-md-7" id="validationRuleParams"></div>
                                    </div>
                                    <div class="mt-2">
                                        <label class="form-label" for="validationRuleCustomMessage">Custom message (optional)</label>
                                        <input type="text" id="validationRuleCustomMessage" class="form-control form-control-sm" placeholder="Override the default error message" />
                                    </div>
                                    <div class="mt-3">
                                        <button type="button" id="btnAddValidationRule" class="btn btn-sm btn-outline-primary">
                                            <i class="fa-solid fa-plus me-1"></i>Add to list
                                        </button>
                                    </div>
                                </div>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" id="btnSaveValidationRules" class="btn btn-primary">
                                <i class="fa-solid fa-check me-1"></i>Apply Rules
                            </button>
                        </div>
                    </div>
                </div>
            </div>`);

        modalInstance = bootstrap.Modal.getOrCreateInstance(document.getElementById('validationRuleModal'));
    }

    function bindGlobalEvents() {
        $(document).on('click', '.btn-edit-validation', function () {
            openEditor($(this).closest('tr'));
        });

        $('#validationRuleTypeSelect').on('change', renderParameterInputs);
        $('#btnAddValidationRule').on('click', addRuleFromForm);
        $('#btnSaveValidationRules').on('click', saveRulesToRow);
        $('#validationRuleModal').on('hidden.bs.modal', function () {
            activeRow = null;
            activeRules = [];
        });
    }

    function populateRuleTypeSelect() {
        const $select = $('#validationRuleTypeSelect').empty();
        const grouped = groupBy(catalog, item => item.category || item.Category || 'Other');

        Object.keys(grouped).forEach(function (category) {
            const $group = $('<optgroup>').attr('label', category);
            grouped[category].forEach(function (item) {
                const type = item.type || item.Type;
                const label = item.label || item.Label;
                $group.append($('<option>').val(type).text(label));
            });
            $select.append($group);
        });

        renderParameterInputs();
    }

    function renderParameterInputs() {
        const type = $('#validationRuleTypeSelect').val();
        const def = catalog.find(function (item) { return (item.type || item.Type) === type; });
        const $params = $('#validationRuleParams').empty();

        if (!def || !(def.parameters || def.Parameters || []).length) {
            $params.append('<div class="text-muted small py-2">No parameters required for this rule.</div>');
            return;
        }

        (def.parameters || def.Parameters).forEach(function (param) {
            const name = param.name || param.Name;
            const label = param.label || param.Label;
            const inputType = param.inputType || param.InputType || 'text';
            const placeholder = param.placeholder || param.Placeholder || '';
            const required = param.required || param.Required;

            const $col = $('<div class="col-md-6">');
            $col.append($('<label class="form-label">').attr('for', 'vr-param-' + name).text(label + (required ? ' *' : '')));

            if (inputType === 'select') {
                const $select = $('<select class="form-select form-select-sm vr-param">')
                    .attr('id', 'vr-param-' + name)
                    .attr('data-param', name);
                (param.options || param.Options || []).forEach(function (opt) {
                    $select.append($('<option>')
                        .val(opt.value || opt.Value)
                        .text(opt.label || opt.Label));
                });
                $col.append($select);
            } else {
                $col.append($('<input>')
                    .attr('type', inputType)
                    .attr('id', 'vr-param-' + name)
                    .attr('data-param', name)
                    .addClass('form-control form-control-sm vr-param')
                    .attr('placeholder', placeholder));
            }

            $params.append($col);
        });
    }

    function openEditor($row) {
        activeRow = $row;
        const propertyName = $row.find('.field-prop').val()?.trim() || 'Field';
        const stored = getRowValidationRule($row) || '';
        activeRules = parseValidationRule(stored).rules.slice();

        $('#validationRuleFieldName').text(propertyName);
        $('#validationRuleCustomMessage').val('');
        renderRuleList();
        renderParameterInputs();
        modalInstance.show();
    }

    function renderRuleList() {
        const $list = $('#validationRuleList').empty();

        if (!activeRules.length) {
            $list.append('<div class="validation-rule-empty text-muted">No validation rules configured.</div>');
            return;
        }

        activeRules.forEach(function (rule, index) {
            const summary = summarizeRule(rule);
            $list.append(`
                <div class="validation-rule-item" data-index="${index}">
                    <div class="validation-rule-item-body">
                        <span class="validation-rule-item-type">${esc(getRuleLabel(rule.type))}</span>
                        <span class="validation-rule-item-summary">${esc(summary)}</span>
                        ${rule.message ? `<span class="validation-rule-item-message">${esc(rule.message)}</span>` : ''}
                    </div>
                    <button type="button" class="btn btn-sm btn-outline-danger btn-remove-validation-rule" title="Remove" aria-label="Remove">
                        <i class="fa-solid fa-trash"></i>
                    </button>
                </div>`);
        });

        $list.find('.btn-remove-validation-rule').on('click', function () {
            const index = parseInt($(this).closest('.validation-rule-item').data('index'), 10);
            activeRules.splice(index, 1);
            renderRuleList();
        });
    }

    function addRuleFromForm() {
        const type = $('#validationRuleTypeSelect').val();
        if (!type) return;

        const rule = { type: type };
        const message = $('#validationRuleCustomMessage').val()?.trim();
        if (message) rule.message = message;

        const def = catalog.find(function (item) { return (item.type || item.Type) === type; });
        let valid = true;
        $('.vr-param').each(function () {
            const name = $(this).data('param');
            let value = $(this).val()?.trim();
            const paramDef = (def?.parameters || def?.Parameters || []).find(function (p) {
                return (p.name || p.Name) === name;
            });
            const isRequired = paramDef && (paramDef.required || paramDef.Required);

            if (!value) {
                if (isRequired) {
                    valid = false;
                    return false;
                }
                return;
            }

            if (name === 'min' || name === 'max') rule[name] = value;
            else if (name === 'operator') rule.operator = value;
            else if (name === 'otherField') rule.otherField = value;
            else if (name === 'columns') rule.columns = value;
            else rule.value = value;
        });

        const requiredParams = (def?.parameters || def?.Parameters || []).filter(function (p) {
            return p.required || p.Required;
        });

        if (requiredParams.length > 0 && !valid) {
            notify('Please fill in all required rule parameters.', 'warning');
            return;
        }

        if (type === 'email' || type === 'phone' || type === 'url' || type === 'unique') {
            const exists = activeRules.some(function (r) { return r.type === type; });
            if (exists) {
                notify('This rule type is already added.', 'warning');
                return;
            }
        }

        activeRules.push(rule);
        $('#validationRuleCustomMessage').val('');
        $('.vr-param').val('');
        renderRuleList();
    }

    function saveRulesToRow() {
        if (!activeRow) return;

        const serialized = serializeValidationRule({ rules: activeRules });
        setRowValidationRule(activeRow, serialized);
        modalInstance.hide();
        activeRow.closest('table').trigger('change');
    }

    function renderValidationCell() {
        return `
            <td class="field-validation-cell">
                <div class="validation-rule-cell">
                    <input type="hidden" class="field-validation" value="" />
                    <div class="validation-rule-summary text-muted">None</div>
                    <button type="button" class="btn btn-sm btn-outline-primary btn-edit-validation" title="Configure validation rules">
                        <i class="fa-solid fa-shield-halved me-1"></i>Rules
                    </button>
                </div>
            </td>`;
    }

    function normalizeStoredRule(stored) {
        if (!stored) return '';

        stored = String(stored).trim();
        if (stored.includes('&quot;') || stored.includes('&amp;') || stored.includes('&lt;') || stored.includes('&gt;')) {
            const textarea = document.createElement('textarea');
            textarea.innerHTML = stored;
            stored = textarea.value.trim();
        }

        return stored;
    }

    function setRowValidationRule($row, storedValue) {
        const normalized = normalizeStoredRule(storedValue);
        $row.find('.field-validation').val(normalized);
        updateRowSummary($row);
    }

    function getRowValidationRule($row) {
        const val = normalizeStoredRule($row.find('.field-validation').val());
        return val || null;
    }

    function updateRowSummary($row) {
        const stored = getRowValidationRule($row) || '';
        const summary = summarizeValidationRule(stored);
        const $summary = $row.find('.validation-rule-summary');
        $summary.text(summary || 'None');
        $summary.toggleClass('text-muted', !summary);
    }

    function parseValidationRule(stored) {
        if (!stored) return { rules: [] };

        stored = normalizeStoredRule(stored);
        if (stored.startsWith('{')) {
            try {
                const parsed = JSON.parse(stored);
                return { rules: Array.isArray(parsed.rules) ? parsed.rules : [] };
            } catch {
                return { rules: [] };
            }
        }

        const rules = [];
        stored.split('|').forEach(function (part) {
            part = part.trim();
            if (!part) return;

            const colon = part.indexOf(':');
            const name = (colon >= 0 ? part.substring(0, colon) : part).trim().toLowerCase();
            const value = colon >= 0 ? part.substring(colon + 1).trim() : null;

            if (name === 'maxlength' && value) rules.push({ type: 'maxLength', value: value });
            else if (name === 'minlength' && value) rules.push({ type: 'minLength', value: value });
            else if (name === 'range' && value) {
                const dash = value.indexOf('-');
                if (dash >= 0) {
                    rules.push({ type: 'range', min: value.substring(0, dash).trim(), max: value.substring(dash + 1).trim() });
                }
            } else if (name === 'regex' && value) rules.push({ type: 'regex', value: value });
            else if (name === 'email') rules.push({ type: 'email' });
            else if (name === 'phone') rules.push({ type: 'phone' });
            else if (name === 'url') rules.push({ type: 'url' });
        });

        return { rules: rules };
    }

    function serializeValidationRule(ruleSet) {
        if (!ruleSet?.rules?.length) return '';
        return JSON.stringify({ rules: ruleSet.rules });
    }

    function summarizeValidationRule(stored) {
        const ruleSet = parseValidationRule(stored);
        if (!ruleSet.rules.length) return '';
        return ruleSet.rules.map(summarizeRule).join(' · ');
    }

    function summarizeRule(rule) {
        switch ((rule.type || '').toLowerCase()) {
            case 'maxlength': return 'Max ' + rule.value + ' chars';
            case 'minlength': return 'Min ' + rule.value + ' chars';
            case 'range': return 'Range ' + rule.min + '–' + rule.max;
            case 'email': return 'Email format';
            case 'phone': return 'Phone format';
            case 'url': return 'URL format';
            case 'regex': return 'Pattern match';
            case 'comparefield': return compareLabel(rule.operator) + ' ' + (rule.otherField || '');
            case 'unique':
                return rule.columns ? 'Unique (' + rule.columns + ')' : 'Unique in database';
            default: return rule.type || 'Rule';
        }
    }

    function compareLabel(op) {
        switch ((op || '').toLowerCase()) {
            case 'gt': return 'Greater than';
            case 'gte': return 'Greater or equal';
            case 'lt': return 'Less than';
            case 'lte': return 'Less or equal';
            case 'equal': return 'Equal to';
            case 'notequal': return 'Not equal to';
            default: return 'Compare';
        }
    }

    function getRuleLabel(type) {
        const def = catalog.find(function (item) { return (item.type || item.Type) === type; });
        return def ? (def.label || def.Label) : type;
    }

    function groupBy(items, keyFn) {
        return items.reduce(function (acc, item) {
            const key = keyFn(item);
            acc[key] = acc[key] || [];
            acc[key].push(item);
            return acc;
        }, {});
    }

    function notify(message, type) {
        if (typeof MetaForgeUi !== 'undefined') {
            MetaForgeUi.showAlert(message, type || 'danger');
            return;
        }
        window.alert(message);
    }

    function esc(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/"/g, '&quot;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }

    return {
        init: init,
        renderValidationCell: renderValidationCell,
        setRowValidationRule: setRowValidationRule,
        getRowValidationRule: getRowValidationRule,
        updateRowSummary: updateRowSummary,
        parseValidationRule: parseValidationRule,
        serializeValidationRule: serializeValidationRule,
        summarizeValidationRule: summarizeValidationRule
    };
})();
