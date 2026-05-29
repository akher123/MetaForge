/**
 * Visual conditional rule builder for Form Builder field rows.
 */
const ConditionalRuleBuilder = (function () {
    let catalog = { actions: [], operators: [] };
    let modalInstance = null;
    let activeRow = null;
    let activeRules = [];
    let availableFields = [];

    function notify(message, type) {
        if (typeof MetaForgeUi !== 'undefined') {
            MetaForgeUi.showAlert(message, type || 'danger');
            return;
        }
        window.alert(message);
    }

    function init() {
        catalog = getFallbackCatalog();
        ensureModal();
        bindGlobalEvents();
        refreshSelects();
        loadCatalog();
        initColumnHeaderTooltips();
    }

    function disposeTooltip($el) {
        const el = $el.get(0);
        if (!el) return;

        const instance = bootstrap.Tooltip.getInstance(el);
        if (instance) {
            instance.dispose();
        }
    }

    function applyTooltip($el, content, options) {
        disposeTooltip($el);

        if (!content) {
            $el.removeAttr('data-bs-toggle data-bs-html data-bs-placement data-bs-custom-class data-bs-sanitize title tabindex aria-label');
            return;
        }

        options = options || {};
        const el = $el.get(0);
        $el.attr({
            'data-bs-toggle': 'tooltip',
            'data-bs-html': 'true',
            'data-bs-placement': options.placement || 'left',
            'data-bs-custom-class': options.customClass || 'conditional-rule-tooltip',
            'data-bs-sanitize': 'false',
            'title': content,
            'tabindex': '0'
        });

        if (options.ariaLabel) {
            $el.attr('aria-label', options.ariaLabel);
        }

        bootstrap.Tooltip.getOrCreateInstance(el, {
            html: true,
            sanitize: false,
            placement: options.placement || 'left',
            customClass: options.customClass || 'conditional-rule-tooltip'
        });
    }

    function initColumnHeaderTooltips() {
        const headerTooltip = FieldConditionalEngine.getColumnHeaderTooltip();

        $('.field-conditional-col .conditional-col-help').each(function () {
            applyTooltip($(this), headerTooltip, {
                placement: 'bottom',
                ariaLabel: 'Conditional rules reference'
            });
        });
    }

    function loadCatalog() {
        $.getJSON('/api/metaforge/formconfig/conditional-rules')
            .done(function (data) {
                catalog = {
                    actions: data.actions || data.Actions || [],
                    operators: data.operators || data.Operators || []
                };
                if (!catalog.actions.length || !catalog.operators.length) {
                    catalog = getFallbackCatalog();
                }
                refreshSelects();
            })
            .fail(function () {
                catalog = getFallbackCatalog();
                refreshSelects();
            });
    }

    function getFallbackCatalog() {
        return {
            actions: [
                { action: 'show', label: 'Show field', description: 'Make this field visible when the condition is true.' },
                { action: 'hide', label: 'Hide field', description: 'Hide this field when the condition is true.' },
                { action: 'enable', label: 'Enable field', description: 'Allow editing when the condition is true.' },
                { action: 'disable', label: 'Disable field', description: 'Make read-only when the condition is true.' },
                { action: 'require', label: 'Require field', description: 'Mark as required when the condition is true.' },
                { action: 'optional', label: 'Make optional', description: 'Remove required flag when the condition is true.' }
            ],
            operators: [
                { operator: 'equals', label: 'Equals', description: 'Source field value matches the comparison value.', requiresValue: true },
                { operator: 'notequals', label: 'Does not equal', description: 'Source field value is different from the comparison value.', requiresValue: true },
                { operator: 'empty', label: 'Is empty', description: 'Source field has no value.', requiresValue: false },
                { operator: 'notempty', label: 'Is not empty', description: 'Source field has any value entered.', requiresValue: false },
                { operator: 'contains', label: 'Contains', description: 'Source field text includes the comparison value.', requiresValue: true },
                { operator: 'gt', label: 'Greater than', description: 'Source field number is greater than the value.', requiresValue: true },
                { operator: 'gte', label: 'Greater than or equal', description: 'Source field number is greater than or equal to the value.', requiresValue: true },
                { operator: 'lt', label: 'Less than', description: 'Source field number is less than the value.', requiresValue: true },
                { operator: 'lte', label: 'Less than or equal', description: 'Source field number is less than or equal to the value.', requiresValue: true }
            ]
        };
    }

    function ensureModal() {
        if ($('#conditionalRuleModal').length) return;

        $('body').append(`
            <div class="modal fade" id="conditionalRuleModal" tabindex="-1" aria-labelledby="conditionalRuleModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-lg modal-dialog-scrollable">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="conditionalRuleModalLabel">
                                <i class="fa-solid fa-code-branch me-2"></i>Conditional Rules
                            </h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <div class="validation-rule-field-context mb-3">
                                Field: <strong id="conditionalRuleFieldName"></strong>
                            </div>
                            <div class="alert alert-light border py-2 px-3 mb-3">
                                <i class="fa-solid fa-circle-info me-2 text-primary"></i>
                                Rules run top to bottom. When a condition matches, its action is applied to <strong>this field</strong>.
                                Set base flags first (Vis / Req / RO), then add rules to override them at runtime.
                            </div>
                            <div id="conditionalRuleList" class="validation-rule-list mb-3"></div>
                            <div class="validation-rule-add card admin-card">
                                <div class="card-header py-2"><i class="fa-solid fa-plus me-1"></i>Add Rule</div>
                                <div class="card-body">
                                    <div class="row g-2">
                                        <div class="col-md-4">
                                            <label class="form-label" for="conditionalRuleAction">Action</label>
                                            <select id="conditionalRuleAction" class="form-select form-select-sm"></select>
                                            <div id="conditionalRuleActionHelp" class="form-text conditional-rule-help"></div>
                                        </div>
                                        <div class="col-md-4">
                                            <label class="form-label" for="conditionalRuleSourceField">When field</label>
                                            <select id="conditionalRuleSourceField" class="form-select form-select-sm"></select>
                                            <div class="form-text conditional-rule-help">Another field on this form (can be this field).</div>
                                        </div>
                                        <div class="col-md-4">
                                            <label class="form-label" for="conditionalRuleOperator">Operator</label>
                                            <select id="conditionalRuleOperator" class="form-select form-select-sm"></select>
                                            <div id="conditionalRuleOperatorHelp" class="form-text conditional-rule-help"></div>
                                        </div>
                                        <div class="col-md-12" id="conditionalRuleValueWrap">
                                            <label class="form-label" for="conditionalRuleValue">Value</label>
                                            <input type="text" id="conditionalRuleValue" class="form-control form-control-sm" placeholder="e.g. Approved, Ship, 100, true" />
                                            <div class="form-text conditional-rule-help">Comparison value. Not needed for Is empty / Is not empty.</div>
                                        </div>
                                    </div>
                                    <div class="mt-3">
                                        <button type="button" id="btnAddConditionalRule" class="btn btn-sm btn-outline-primary">
                                            <i class="fa-solid fa-plus me-1"></i>Add to list
                                        </button>
                                    </div>
                                </div>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" id="btnSaveConditionalRules" class="btn btn-primary">
                                <i class="fa-solid fa-check me-1"></i>Apply Rules
                            </button>
                        </div>
                    </div>
                </div>
            </div>`);

        modalInstance = bootstrap.Modal.getOrCreateInstance(document.getElementById('conditionalRuleModal'));
    }

    function refreshSelects() {
        populateActionSelect();
        populateOperatorSelect();
        updateActionHelp();
        updateOperatorHelp();
    }

    function findAction(action) {
        return (catalog.actions || []).find(function (item) {
            return (item.action || item.Action) === action;
        });
    }

    function findOperator(op) {
        return (catalog.operators || []).find(function (item) {
            return (item.operator || item.Operator) === op;
        });
    }

    function populateActionSelect() {
        const $select = $('#conditionalRuleAction');
        if (!$select.length) return;

        const current = $select.val();
        $select.empty();

        (catalog.actions || []).forEach(function (item) {
            const action = item.action || item.Action;
            const label = item.label || item.Label;
            $select.append($('<option>').val(action).text(label));
        });

        if (current && $select.find(`option[value="${current}"]`).length) {
            $select.val(current);
        }
    }

    function populateOperatorSelect() {
        const $select = $('#conditionalRuleOperator');
        if (!$select.length) return;

        const current = $select.val();
        $select.empty();

        (catalog.operators || []).forEach(function (item) {
            const op = item.operator || item.Operator;
            const label = item.label || item.Label;
            const requiresValue = item.requiresValue ?? item.RequiresValue ?? true;
            $select.append(
                $('<option>')
                    .val(op)
                    .text(label)
                    .attr('data-requires-value', requiresValue ? 'true' : 'false')
            );
        });

        if (current && $select.find(`option[value="${current}"]`).length) {
            $select.val(current);
        }

        toggleValueInput();
    }

    function populateSourceFieldSelect() {
        const $select = $('#conditionalRuleSourceField');
        if (!$select.length) return;

        const current = $select.val();
        $select.empty();

        if (!availableFields.length) {
            $select.append($('<option>').val('').text('— Add fields to the form first —'));
            return;
        }

        availableFields.forEach(function (name) {
            if (name) {
                $select.append($('<option>').val(name).text(name));
            }
        });

        if (current && $select.find(`option[value="${current}"]`).length) {
            $select.val(current);
        }
    }

    function updateActionHelp() {
        const action = $('#conditionalRuleAction').val();
        const def = findAction(action);
        const text = def?.description || def?.Description || '';
        $('#conditionalRuleActionHelp').text(text);
    }

    function updateOperatorHelp() {
        const op = $('#conditionalRuleOperator').val();
        const def = findOperator(op);
        const text = def?.description || def?.Description || '';
        $('#conditionalRuleOperatorHelp').text(text);
    }

    function toggleValueInput() {
        const $opt = $('#conditionalRuleOperator option:selected');
        const requiresValue = $opt.attr('data-requires-value') !== 'false';
        $('#conditionalRuleValueWrap').toggleClass('d-none', !requiresValue);
    }

    function bindGlobalEvents() {
        $(document).on('click', '.btn-edit-conditional', function () {
            openEditor($(this).closest('tr'));
        });

        $(document).on('change', '#conditionalRuleAction', updateActionHelp);
        $(document).on('change', '#conditionalRuleOperator', function () {
            toggleValueInput();
            updateOperatorHelp();
        });
        $(document).on('click', '#btnAddConditionalRule', addRuleFromForm);
        $(document).on('click', '#btnSaveConditionalRules', saveRulesToRow);
        $('#conditionalRuleModal').on('hidden.bs.modal', function () {
            activeRow = null;
            activeRules = [];
        });
    }

    function collectTableFields($table) {
        const fields = [];
        $table.find('tbody tr').each(function () {
            const prop = $(this).find('.field-prop').val()?.trim();
            if (prop) fields.push(prop);
        });
        return fields;
    }

    function openEditor($row) {
        activeRow = $row;
        const propertyName = $row.find('.field-prop').val()?.trim() || 'Field';
        const stored = getRowConditionalRule($row) || '';
        activeRules = FieldConditionalEngine.parseConditionalRule(stored).rules.slice();

        const $table = $row.closest('table');
        availableFields = collectTableFields($table);

        $('#conditionalRuleFieldName').text(propertyName);
        refreshSelects();
        populateSourceFieldSelect();
        $('#conditionalRuleValue').val('');
        renderRuleList();
        modalInstance.show();
    }

    function renderRuleList() {
        const $list = $('#conditionalRuleList').empty();

        if (!activeRules.length) {
            $list.append('<div class="validation-rule-empty text-muted">No conditional rules configured.</div>');
            return;
        }

        activeRules.forEach(function (rule, index) {
            const summary = FieldConditionalEngine.summarizeRule(rule);
            $list.append(`
                <div class="validation-rule-item" data-index="${index}">
                    <div class="validation-rule-item-body">
                        <span class="validation-rule-item-type">${esc(FieldConditionalEngine.getActionLabel(rule.action))}</span>
                        <span class="validation-rule-item-summary">${esc(summary)}</span>
                    </div>
                    <button type="button" class="btn btn-sm btn-outline-danger btn-remove-conditional-rule" title="Remove" aria-label="Remove">
                        <i class="fa-solid fa-trash"></i>
                    </button>
                </div>`);
        });

        $list.find('.btn-remove-conditional-rule').on('click', function () {
            const index = parseInt($(this).closest('.validation-rule-item').data('index'), 10);
            activeRules.splice(index, 1);
            renderRuleList();
        });
    }

    function addRuleFromForm() {
        const action = $('#conditionalRuleAction').val();
        const sourceField = $('#conditionalRuleSourceField').val()?.trim();
        const operator = $('#conditionalRuleOperator').val();
        const value = $('#conditionalRuleValue').val()?.trim();

        if (!action || !sourceField || !operator) {
            notify('Action, when field, and operator are required.', 'warning');
            return;
        }

        const $opt = $('#conditionalRuleOperator option:selected');
        const requiresValue = $opt.attr('data-requires-value') !== 'false';
        if (requiresValue && !value) {
            notify('A comparison value is required for this operator.', 'warning');
            return;
        }

        activeRules.push({
            action: action,
            sourceField: sourceField,
            operator: operator,
            value: requiresValue ? value : ''
        });

        $('#conditionalRuleValue').val('');
        renderRuleList();
    }

    function saveRulesToRow() {
        if (!activeRow) return;

        const serialized = FieldConditionalEngine.serializeConditionalRule({ rules: activeRules });
        setRowConditionalRule(activeRow, serialized);
        modalInstance.hide();
        activeRow.closest('table').trigger('change');
    }

    function renderConditionalCell() {
        return `
            <td class="field-conditional-cell">
                <div class="validation-rule-cell">
                    <input type="hidden" class="field-conditional" value="" />
                    <div class="conditional-rule-summary text-muted">None</div>
                    <button type="button" class="btn btn-sm btn-outline-info btn-edit-conditional" title="Configure conditional rules">
                        <i class="fa-solid fa-code-branch me-1"></i>Rules
                    </button>
                </div>
            </td>`;
    }

    function normalizeStoredRule(stored) {
        if (!stored) return '';
        stored = String(stored).trim();
        if (stored.includes('&quot;') || stored.includes('&amp;')) {
            const textarea = document.createElement('textarea');
            textarea.innerHTML = stored;
            stored = textarea.value.trim();
        }
        return stored;
    }

    function setRowConditionalRule($row, storedValue) {
        const normalized = normalizeStoredRule(storedValue);
        $row.find('.field-conditional').val(normalized);
        updateRowSummary($row);
    }

    function getRowConditionalRule($row) {
        return normalizeStoredRule($row.find('.field-conditional').val());
    }

    function updateRowSummary($row) {
        const stored = getRowConditionalRule($row);
        const ruleSet = FieldConditionalEngine.parseConditionalRule(stored);
        const hasRules = ruleSet.rules.length > 0;
        const compact = hasRules ? FieldConditionalEngine.summarizeCompact(stored) : '';
        const tooltip = hasRules ? FieldConditionalEngine.formatRulesTooltip(stored) : '';
        const $summary = $row.find('.conditional-rule-summary');

        $summary.text(compact || 'None');
        $summary.toggleClass('text-muted', !hasRules);
        $summary.toggleClass('text-info', hasRules);
        $summary.toggleClass('conditional-rule-summary--active', hasRules);

        applyTooltip($summary, tooltip, {
            placement: 'left',
            ariaLabel: hasRules ? 'Conditional rules: ' + FieldConditionalEngine.summarize(stored) : ''
        });
    }

    function esc(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/"/g, '&quot;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }

    return {
        init,
        renderConditionalCell,
        setRowConditionalRule,
        getRowConditionalRule,
        updateRowSummary,
        initColumnHeaderTooltips
    };
})();
