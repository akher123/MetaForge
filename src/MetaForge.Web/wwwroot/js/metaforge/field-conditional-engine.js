/**
 * Evaluates field conditional rules (show/hide, enable/disable, require/optional).
 */
const FieldConditionalEngine = (function () {
    const ACTIONS = ['show', 'hide', 'enable', 'disable', 'require', 'optional'];
    const OPERATORS = ['equals', 'notequals', 'empty', 'notempty', 'contains', 'gt', 'gte', 'lt', 'lte'];

    function parseConditionalRule(stored) {
        if (!stored) return { rules: [] };

        let text = String(stored).trim();
        if (!text) return { rules: [] };

        if (text.includes('&quot;') || text.includes('&amp;')) {
            const textarea = document.createElement('textarea');
            textarea.innerHTML = text;
            text = textarea.value.trim();
        }

        if (!text.startsWith('{')) return { rules: [] };

        try {
            const parsed = JSON.parse(text);
            const rules = (parsed.rules || parsed.Rules || []).map(normalizeRule);
            return { rules: rules };
        } catch {
            return { rules: [] };
        }
    }

    function normalizeRule(rule) {
        return {
            action: (rule.action || rule.Action || '').toLowerCase(),
            sourceField: rule.sourceField || rule.SourceField || '',
            operator: (rule.operator || rule.Operator || 'equals').toLowerCase(),
            value: rule.value ?? rule.Value ?? ''
        };
    }

    function serializeConditionalRule(ruleSet) {
        if (!ruleSet?.rules?.length) return '';
        return JSON.stringify({ rules: ruleSet.rules });
    }

    function getFieldValue(data, fieldName) {
        if (!data || !fieldName) return undefined;
        return data[fieldName]
            ?? data[fieldName.charAt(0).toLowerCase() + fieldName.slice(1)]
            ?? data[fieldName.charAt(0).toUpperCase() + fieldName.slice(1)];
    }

    function toStringValue(value) {
        if (value == null) return '';
        if (typeof value === 'boolean') return value ? 'true' : 'false';
        return String(value);
    }

    function isEmptyValue(value) {
        if (value == null || value === '') return true;
        if (value === false || value === 0 || value === '0') return true;
        return String(value).trim() === '';
    }

    function equalsValues(strValue, compareValue, rawValue) {
        if (compareValue === 'true' || compareValue === 'false') {
            const boolCompare = compareValue === 'true';
            if (typeof rawValue === 'boolean') return rawValue === boolCompare;
            if (strValue === 'true' || strValue === 'false') return strValue === compareValue;
        }

        const leftNum = parseFloat(strValue);
        const rightNum = parseFloat(compareValue);
        if (!Number.isNaN(leftNum) && !Number.isNaN(rightNum)) {
            return leftNum === rightNum;
        }

        return String(strValue).toLowerCase() === String(compareValue).toLowerCase();
    }

    function compareNumeric(strValue, compareValue, op) {
        const left = parseFloat(strValue);
        const right = parseFloat(compareValue);
        if (Number.isNaN(left) || Number.isNaN(right)) return false;

        switch (op) {
            case 'gt': return left > right;
            case 'gte': return left >= right;
            case 'lt': return left < right;
            case 'lte': return left <= right;
            default: return false;
        }
    }

    function evaluateCondition(rule, data) {
        const sourceField = rule.sourceField || rule.SourceField;
        if (!sourceField) return false;

        const rawValue = getFieldValue(data, sourceField);
        const strValue = toStringValue(rawValue);
        const op = (rule.operator || 'equals').toLowerCase();
        const compareValue = rule.value ?? rule.Value ?? '';

        switch (op) {
            case 'empty':
                return isEmptyValue(rawValue);
            case 'notempty':
                return !isEmptyValue(rawValue);
            case 'contains':
                return strValue.toLowerCase().includes(String(compareValue).toLowerCase());
            case 'notequals':
                return !equalsValues(strValue, compareValue, rawValue);
            case 'gt':
            case 'gte':
            case 'lt':
            case 'lte':
                return compareNumeric(strValue, compareValue, op);
            default:
                return equalsValues(strValue, compareValue, rawValue);
        }
    }

    function evaluateEffectiveState(field, data) {
        const visible = field.IsVisible ?? field.isVisible ?? true;
        const required = field.IsRequired ?? field.isRequired ?? false;
        const readOnly = field.IsReadOnly ?? field.isReadOnly ?? false;

        const state = { visible: visible, required: required, readOnly: readOnly };
        const stored = field.ConditionalRule ?? field.conditionalRule;
        const ruleSet = parseConditionalRule(stored);

        ruleSet.rules.forEach(function (rule) {
            if (!evaluateCondition(rule, data)) return;

            switch ((rule.action || '').toLowerCase()) {
                case 'show': state.visible = true; break;
                case 'hide': state.visible = false; break;
                case 'enable': state.readOnly = false; break;
                case 'disable': state.readOnly = true; break;
                case 'require': state.required = true; break;
                case 'optional': state.required = false; break;
            }
        });

        return state;
    }

    function summarizeRule(rule) {
        const action = rule.action || 'rule';
        const source = rule.sourceField || '?';
        const op = rule.operator || 'equals';

        if (op === 'empty' || op === 'notempty') {
            return `${action} when ${source} ${op}`;
        }

        return `${action} when ${source} ${op} ${rule.value ?? ''}`.trim();
    }

    function summarize(stored) {
        const ruleSet = parseConditionalRule(stored);
        if (!ruleSet.rules.length) return '';
        return ruleSet.rules.map(summarizeRule).join(', ');
    }

    function getActionLabel(action) {
        const labels = {
            show: 'Show',
            hide: 'Hide',
            enable: 'Enable',
            disable: 'Disable',
            require: 'Require',
            optional: 'Optional'
        };
        return labels[(action || '').toLowerCase()] || action;
    }

    function getOperatorLabel(op) {
        const labels = {
            equals: 'equals',
            notequals: 'does not equal',
            empty: 'is empty',
            notempty: 'is not empty',
            contains: 'contains',
            gt: '>',
            gte: '>=',
            lt: '<',
            lte: '<='
        };
        return labels[(op || '').toLowerCase()] || op;
    }

    return {
        ACTIONS,
        OPERATORS,
        parseConditionalRule,
        serializeConditionalRule,
        evaluateCondition,
        evaluateEffectiveState,
        summarize,
        summarizeRule,
        getActionLabel,
        getOperatorLabel,
        getFieldValue,
        toStringValue
    };
})();
