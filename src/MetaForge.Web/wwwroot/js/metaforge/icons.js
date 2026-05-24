/**
 * Font Awesome icon snippets for admin UI actions.
 */
const MetaForgeIcons = (function () {
    function icon(name, extraClass) {
        const cls = extraClass ? ` ${extraClass}` : '';
        return `<i class="fa-solid fa-${name}${cls}"></i>`;
    }

    function only(name) {
        return icon(name);
    }

    return {
        icon,
        only,
        create: only('plus'),
        add: only('plus'),
        edit: only('pen-to-square'),
        update: only('pen-to-square'),
        delete: only('trash'),
        view: only('eye'),
        detail: only('list-ul'),
        save: only('floppy-disk'),
        cancel: only('xmark'),
        back: only('arrow-left'),
        exportExcel: only('file-excel'),
        exportCsv: only('file-csv'),
        sync: only('rotate'),
        user: only('user'),
        userPlus: only('user-plus'),
        role: only('shield-halved'),
        permission: only('key'),
        config: only('gear'),
        open: only('arrow-up-right-from-square'),
        header: only('file-lines'),
        lines: only('table-list'),
        signOut: only('right-from-bracket'),
        apply: only('check'),
        removeLine: only('trash-can'),
        download: only('download'),
        database: only('database'),
        invoice: only('file-invoice'),
        selectAll: only('check-double'),
        clearAll: only('eraser')
    };
})();
