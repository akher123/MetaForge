/**
 * DataTables — Bootstrap 5 defaults (requires dataTables.bootstrap5.js).
 */
(function () {
    if (typeof $ === 'undefined' || !$.fn.dataTable) {
        return;
    }

    $.extend(true, $.fn.dataTable.defaults, {
        dom:
            "<'row align-items-center g-2 datatables-toolbar'<'col-sm-6'l><'col-sm-6'f>>" +
            "<'row'<'col-12'tr>>" +
            "<'row align-items-center g-2 datatables-footer'<'col-sm-6'i><'col-sm-6'p>>",
        renderer: 'bootstrap'
    });

    function applyBootstrapFormControls($wrapper) {
        $wrapper.find('.dataTables_length select')
            .addClass('form-select form-select-sm');
        $wrapper.find('.dataTables_filter input')
            .addClass('form-control form-control-sm');
        applySearchIcon($wrapper);
    }

    function applySearchIcon($wrapper) {
        $wrapper.find('.dataTables_filter input').each(function () {
            const $input = $(this);
            if ($input.parent().hasClass('datatables-search-wrap')) {
                return;
            }
            $input.wrap('<span class="datatables-search-wrap"></span>');
            $('<i class="fa-solid fa-magnifying-glass datatables-search-icon" aria-hidden="true"></i>')
                .insertBefore($input);
            $input.attr('aria-label', 'Search table');
        });
    }

    $(document).on('init.dt', function (_e, settings) {
        const api = new $.fn.dataTable.Api(settings);
        applyBootstrapFormControls($(api.table().container()));
    });

    $(document).on('draw.dt', function (_e, settings) {
        const api = new $.fn.dataTable.Api(settings);
        applyBootstrapFormControls($(api.table().container()));
    });
})();
