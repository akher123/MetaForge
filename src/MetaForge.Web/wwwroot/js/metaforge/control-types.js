/**
 * Shared dynamic form control type constants and helpers.
 */
const MetaForgeControlTypes = (function () {
    const TextBox = 'TextBox';
    const TextArea = 'TextArea';
    const RichText = 'RichText';
    const Number = 'Number';
    const Date = 'Date';
    const DateTime = 'DateTime';
    const Checkbox = 'Checkbox';
    const Dropdown = 'Dropdown';
    const Autocomplete = 'Autocomplete';
    const MultiSelect = 'MultiSelect';
    const Radio = 'Radio';
    const FileUpload = 'FileUpload';
    const Hidden = 'Hidden';

    const ALL = [
        TextBox, TextArea, RichText, Number, Date, DateTime,
        Checkbox, Dropdown, Autocomplete, MultiSelect, Radio, FileUpload, Hidden
    ];

    function normalize(controlType) {
        const value = (controlType ?? TextBox).toString();
        return ALL.includes(value) ? value : TextBox;
    }

    function isRichText(controlType) {
        return normalize(controlType) === RichText;
    }

    function isFullWidth(controlType) {
        const ct = normalize(controlType);
        return ct === TextArea || ct === RichText || ct === Checkbox || ct === FileUpload;
    }

    function isSingleLookup(controlType) {
        const ct = normalize(controlType);
        return ct === Dropdown || ct === Autocomplete;
    }

    function isMultiSelect(controlType) {
        return normalize(controlType) === MultiSelect;
    }

    function isLookup(controlType) {
        return isSingleLookup(controlType);
    }

    function isLookupOrMultiSelect(controlType) {
        const ct = normalize(controlType);
        return ct === Dropdown || ct === Autocomplete || ct === MultiSelect;
    }

    function inferLookupEntityFromProperty(propertyName) {
        if (!propertyName) return '';
        if (propertyName.endsWith('Ids') && propertyName.length > 3) {
            return propertyName.slice(0, -3);
        }
        if (propertyName.endsWith('Id') && propertyName !== 'Id') {
            return propertyName.slice(0, -2);
        }
        return '';
    }

    function inferMappingDefaults(propertyName, masterEntity) {
        if (!propertyName || !masterEntity || !propertyName.endsWith('Ids') || propertyName.length <= 3) {
            return null;
        }

        const related = propertyName.slice(0, -3);
        return {
            mappingEntity: masterEntity + related,
            mappingParentKey: masterEntity + 'Id',
            mappingRelatedKey: related + 'Id'
        };
    }

    return {
        TextBox,
        TextArea,
        RichText,
        Number,
        Date,
        DateTime,
        Checkbox,
        Dropdown,
        Autocomplete,
        MultiSelect,
        Radio,
        FileUpload,
        Hidden,
        ALL,
        normalize,
        isRichText,
        isFullWidth,
        isLookup,
        isSingleLookup,
        isMultiSelect,
        isLookupOrMultiSelect,
        inferLookupEntityFromProperty,
        inferMappingDefaults
    };
})();
