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
    const Radio = 'Radio';
    const FileUpload = 'FileUpload';
    const Hidden = 'Hidden';

    const ALL = [
        TextBox, TextArea, RichText, Number, Date, DateTime,
        Checkbox, Dropdown, Autocomplete, Radio, FileUpload, Hidden
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

    function isLookup(controlType) {
        const ct = normalize(controlType);
        return ct === Dropdown || ct === Autocomplete;
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
        Radio,
        FileUpload,
        Hidden,
        ALL,
        normalize,
        isRichText,
        isFullWidth,
        isLookup
    };
})();
