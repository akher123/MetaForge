/**
 * Multi-table tree grid with paging, search, and per-level forms.
 */
const DynamicMultiTreeGrid = (function () {
    let config = {};
    let levels = [];
    let permissions = {};
    let levelPermissions = {};
    let state = {
        page: 1,
        pageSize: 25,
        searchTerm: '',
        searchLevel: -1,
        sortColumn: null,
        sortDescending: false,
        selected: null,
        expanded: new Map(),
        loading: false,
        searchMode: false
    };

    function perm() {
        return MetaForgePermissions.createApi(permissions);
    }

    function levelPerm(entityName) {
        const key = entityName ?? '';
        const match = Object.keys(levelPermissions).find(k => k.toLowerCase() === key.toLowerCase());
        return MetaForgePermissions.createApi(match ? levelPermissions[match] : {});
    }

    function canCreateEntity(entityName) {
        return perm().canCreate() && levelPerm(entityName).canCreate();
    }

    function canEditEntity(entityName) {
        return perm().canEdit() && levelPerm(entityName).canEdit();
    }

    function canDeleteEntity(entityName) {
        return perm().canDelete() && levelPerm(entityName).canDelete();
    }

    function canOpenEntity(entityName) {
        const entity = levelPerm(entityName);
        return perm().canView() && (entity.canView() || entity.canEdit());
    }

    function getLevel(index) {
        return levels.find(l => (l.LevelIndex ?? l.levelIndex) === index);
    }

    function getLevelDisplayColumns(levelIndex) {
        const level = getLevel(levelIndex);
        if (!level) return [];

        const configured = level.DisplayColumns ?? level.displayColumns ?? [];
        if (configured.length > 0) {
            return configured.map(column => ({
                propertyName: column.PropertyName ?? column.propertyName ?? '',
                label: column.Label ?? column.label ?? column.PropertyName ?? column.propertyName ?? ''
            })).filter(column => column.propertyName);
        }

        const displayColumn = level.DisplayColumn ?? level.displayColumn ?? 'Name';
        return displayColumn
            .split(',')
            .map(part => part.trim())
            .filter(Boolean)
            .map(propertyName => ({ propertyName, label: propertyName }));
    }

    function getTreeColumnDefs() {
        const map = new Map();

        levels.forEach(level => {
            const levelIndex = level.LevelIndex ?? level.levelIndex ?? 0;
            getLevelDisplayColumns(levelIndex).forEach(column => {
                const key = column.propertyName.toLowerCase();
                if (!map.has(key)) {
                    map.set(key, column);
                }
            });
        });

        if (map.size === 0) {
            map.set('name', { propertyName: 'Name', label: 'Name' });
        }

        return Array.from(map.values());
    }

    function getColSpan() {
        return 1 + getTreeColumnDefs().length + 2;
    }

    function resolveDataValue(data, propertyName) {
        if (!data || !propertyName) return '';

        if (Object.prototype.hasOwnProperty.call(data, propertyName)) {
            return data[propertyName];
        }

        const key = Object.keys(data).find(k => k.toLowerCase() === propertyName.toLowerCase());
        return key != null ? data[key] : '';
    }

    function formatCellValue(value) {
        if (value == null || value === '') return '';
        if (typeof value === 'boolean') return value ? 'Yes' : 'No';
        return String(value);
    }

    function renderDisplayCells(node, levelIndex, depth) {
        const levelCols = getLevelDisplayColumns(levelIndex);
        const allCols = getTreeColumnDefs();
        const data = node.Data ?? node.data ?? {};
        let firstLevelColRendered = false;

        return allCols.map(column => {
            const inLevel = levelCols.some(levelCol =>
                levelCol.propertyName.toLowerCase() === column.propertyName.toLowerCase());

            if (!inLevel) {
                return '<td class="module-tree-col-extra text-muted">—</td>';
            }

            let value = formatCellValue(resolveDataValue(data, column.propertyName));
            if (!value && levelCols[0]?.propertyName?.toLowerCase() === column.propertyName.toLowerCase()) {
                value = (node.Label ?? node.label ?? '').toString();
            }

            const isFirst = !firstLevelColRendered;
            firstLevelColRendered = true;
            const indent = isFirst && depth > 0 ? ` style="padding-left:${depth * 20}px"` : '';
            const cellClass = isFirst ? 'module-tree-label-cell' : 'module-tree-data-cell';
            const display = value ? esc(value) : '<span class="text-muted">—</span>';

            return `<td class="${cellClass}"${indent}>${display}</td>`;
        }).join('');
    }

    function buildTableHeader() {
        const cols = getTreeColumnDefs();
        const $row = $('#multiTreeGridHeadRow');
        if (!$row.length) return;

        let html = '<th class="module-tree-col-expand" scope="col"></th>';
        cols.forEach(column => {
            html += `<th scope="col">
                <button type="button" class="btn btn-sm btn-link p-0 module-tree-sort" data-sort="${esc(column.propertyName)}">
                    ${esc(column.label)}
                    <i class="fa-solid fa-sort module-tree-sort-icon" aria-hidden="true"></i>
                </button>
            </th>`;
        });
        html += '<th class="module-tree-col-type d-none d-md-table-cell" scope="col">Type</th>';
        html += '<th class="module-tree-col-actions text-center" scope="col">Actions</th>';
        $row.html(html);
        updateSortIcons();
    }

    function compareNodes(a, b) {
        const dataA = a.Data ?? a.data ?? {};
        const dataB = b.Data ?? b.data ?? {};

        if (state.sortColumn) {
            const aVal = formatCellValue(resolveDataValue(dataA, state.sortColumn)
                || (a.Label ?? a.label ?? '')).toLowerCase();
            const bVal = formatCellValue(resolveDataValue(dataB, state.sortColumn)
                || (b.Label ?? b.label ?? '')).toLowerCase();
            if (state.sortDescending) return bVal.localeCompare(aVal);
            return aVal.localeCompare(bVal);
        }

        const aLabel = (a.Label ?? a.label ?? '').toString().toLowerCase();
        const bLabel = (b.Label ?? b.label ?? '').toString().toLowerCase();
        if (state.sortDescending) return bLabel.localeCompare(aLabel);
        return aLabel.localeCompare(bLabel);
    }

    function esc(text) {
        return $('<div>').text(text ?? '').html();
    }

    function notify(message, type) {
        if (typeof MetaForgeUi !== 'undefined') {
            MetaForgeUi.showAlert(message, type || 'danger');
            return;
        }
        window.alert(message);
    }

    function setLoading(isLoading) {
        state.loading = isLoading;
        if (config.loadingSelector) {
            $(config.loadingSelector).toggleClass('d-none', !isLoading);
        }
        $(config.tableBodySelector).closest('table').toggleClass('opacity-50', isLoading);
    }

    function updateSearchUi() {
        const hasTerm = !!state.searchTerm;
        if (config.searchClearSelector) {
            $(config.searchClearSelector).toggleClass('d-none', !hasTerm);
        }
        if (config.searchSelector) {
            $(config.searchSelector).toggleClass('module-tree-search-active', hasTerm);
        }
    }

    function updateSortIcons() {
        $('.module-tree-sort-icon').removeClass('fa-sort fa-sort-up fa-sort-down');
        $('.module-tree-sort').each(function () {
            const col = $(this).data('sort');
            const $icon = $(this).find('.module-tree-sort-icon');
            if (state.sortColumn && col && col.toLowerCase() === state.sortColumn.toLowerCase()) {
                $icon.addClass(state.sortDescending ? 'fa-sort-down' : 'fa-sort-up');
            } else {
                $icon.addClass('fa-sort');
            }
        });
    }

    function updateLevelHeader() {
        const rootLevel = getLevel(0);
        const rootName = rootLevel?.EntityName ?? rootLevel?.entityName ?? 'Root';

        if (state.searchMode && state.searchTerm) {
            if (state.searchLevel === -1) {
                $(config.levelLabelSelector).text('Search results');
                $(config.levelHintSelector).text(`Matching "${state.searchTerm}" across all levels`);
            } else {
                const level = getLevel(state.searchLevel);
                const name = level?.EntityName ?? level?.entityName ?? `Level ${state.searchLevel}`;
                $(config.levelLabelSelector).text(`${name} search`);
                $(config.levelHintSelector).text(`Matching "${state.searchTerm}" in ${name}`);
            }
            return;
        }

        $(config.levelLabelSelector).text(`${rootName} (root level)`);
        $(config.levelHintSelector).text('Expand nodes to browse child levels');
    }

    function fetchLevel(levelIndex, parentId, page, searchTerm) {
        return $.ajax({
            url: `/api/metaforge/tree/${config.formCode}/level`,
            method: 'POST',
            contentType: 'application/json',
            metaforgeProgress: false,
            data: JSON.stringify({
                FormCode: config.formCode,
                LevelIndex: levelIndex,
                ParentId: parentId ?? null,
                Page: page,
                PageSize: state.pageSize,
                SearchTerm: (searchTerm ?? state.searchTerm) || '',
                SortColumn: state.sortColumn,
                SortDescending: state.sortDescending
            })
        });
    }

    function entityBadge(entityName) {
        return `<span class="badge module-tree-entity-badge">${esc(entityName)}</span>`;
    }

    function treeRowActions(childLevel, nodeEntityName) {
        const openBtn = canOpenEntity(nodeEntityName)
            ? `<button type="button" class="btn btn-sm btn-outline-primary btn-icon btn-action-edit module-tree-select" title="Open" aria-label="Open">${MetaForgeIcons.edit}</button>`
            : '';
        const childEntity = childLevel?.EntityName ?? childLevel?.entityName;
        const addBtn = childLevel && canCreateEntity(childEntity)
            ? `<button type="button" class="btn btn-sm btn-icon btn-action-add module-tree-add-child" title="Add child" aria-label="Add child">${MetaForgeIcons.add}</button>`
            : '';
        if (!openBtn && !addBtn) {
            return '<span class="text-muted small">—</span>';
        }
        return `<div class="module-tree-row-actions">${openBtn}${addBtn}</div>`;
    }

    function appendSearchRow($container, node) {
        const id = node.Id ?? node.id;
        const levelIndex = node.LevelIndex ?? node.levelIndex ?? 0;
        const entityName = node.EntityName ?? node.entityName ?? '';
        const hasChildren = node.HasChildren ?? node.hasChildren;

        const $row = $(`
            <tr class="module-tree-row module-tree-row--search" data-id="${id}" data-level="${levelIndex}" data-entity="${esc(entityName)}">
                <td class="module-tree-col-expand">
                    ${hasChildren
                ? '<i class="fa-solid fa-folder text-warning" aria-hidden="true"></i>'
                : '<i class="fa-regular fa-file text-muted" aria-hidden="true"></i>'}
                </td>
                ${renderDisplayCells(node, levelIndex, 0)}
                <td class="module-tree-col-type d-none d-md-table-cell">${entityBadge(entityName)}</td>
                <td class="module-tree-col-actions text-center">
                    ${treeRowActions(null, entityName)}
                </td>
            </tr>`);

        $container.append($row);
    }

    function appendNodeRow($container, node, depth, parentExpandKey) {
        const id = node.Id ?? node.id;
        const hasChildren = node.HasChildren ?? node.hasChildren;
        const levelIndex = node.LevelIndex ?? node.levelIndex ?? 0;
        const entityName = node.EntityName ?? node.entityName ?? '';
        const expandKey = parentExpandKey ? `${parentExpandKey}/${levelIndex}:${id}` : `${levelIndex}:${id}`;
        const isExpanded = state.expanded.get(expandKey) === true;
        const childLevel = getLevel(levelIndex + 1);

        const $row = $(`
            <tr class="module-tree-row" data-id="${id}" data-level="${levelIndex}" data-entity="${esc(entityName)}" data-expand-key="${esc(expandKey)}" data-depth="${depth}">
                <td class="module-tree-col-expand">
                    ${hasChildren
                ? `<button type="button" class="btn btn-sm btn-link p-0 module-tree-expand" aria-label="${isExpanded ? 'Collapse' : 'Expand'}">
                        <i class="fa-solid fa-chevron-${isExpanded ? 'down' : 'right'}"></i>
                   </button>`
                : '<span class="module-tree-leaf-spacer"></span>'}
                </td>
                ${renderDisplayCells(node, levelIndex, depth)}
                <td class="module-tree-col-type d-none d-md-table-cell">${entityBadge(entityName)}</td>
                <td class="module-tree-col-actions text-center">
                    ${treeRowActions(childLevel, entityName)}
                </td>
            </tr>`);

        $container.append($row);

        if (hasChildren && isExpanded) {
            const $childBlock = $(`
                <tr class="module-tree-child-block" data-parent-expand-key="${esc(expandKey)}">
                    <td colspan="${getColSpan()}" class="p-0">
                        <table class="table table-sm mb-0 module-tree-child-table"><tbody></tbody></table>
                    </td>
                </tr>`);
            $container.append($childBlock);
            const cached = state.expanded.get(`${expandKey}:data`);
            if (cached) {
                cached.forEach(child => appendNodeRow($childBlock.find('tbody'), child, depth + 1, expandKey));
            }
        }
    }

    function renderPagedResult($tbody, response, renderRowFn) {
        const items = response.Items ?? response.items ?? [];
        const total = response.TotalCount ?? response.totalCount ?? 0;
        const totalPages = Math.max(1, Math.ceil(total / state.pageSize));

        $(config.emptySelector).toggleClass('d-none', items.length > 0);
        $(config.pageInfoSelector).text(`Page ${state.page} of ${totalPages} (${total} items)`);
        $(config.prevSelector).prop('disabled', state.page <= 1);
        $(config.nextSelector).prop('disabled', state.page >= totalPages);

        if (config.emptyMessageSelector) {
            const msg = state.searchMode && state.searchTerm
                ? 'Try a different search term or level.'
                : 'No nodes at this level.';
            $(config.emptyMessageSelector).text(msg);
        }

        items.forEach(node => renderRowFn($tbody, node));
    }

    function renderSearchResults(responses) {
        const $tbody = $(config.tableBodySelector).empty();
        let total = 0;
        const items = [];

        responses.forEach(response => {
            const levelItems = response.Items ?? response.items ?? [];
            const levelTotal = response.TotalCount ?? response.totalCount ?? 0;
            total += levelTotal;
            items.push(...levelItems);
        });

        items.sort(compareNodes);

        const start = (state.page - 1) * state.pageSize;
        const pageItems = items.slice(start, start + state.pageSize);
        const totalPages = Math.max(1, Math.ceil(items.length / state.pageSize));

        $(config.emptySelector).toggleClass('d-none', pageItems.length > 0);
        $(config.pageInfoSelector).text(`Page ${state.page} of ${totalPages} (${total} matches)`);
        $(config.prevSelector).prop('disabled', state.page <= 1);
        $(config.nextSelector).prop('disabled', state.page >= totalPages);

        if (config.emptyMessageSelector) {
            $(config.emptyMessageSelector).text('Try a different search term or level.');
        }

        pageItems.forEach(node => appendSearchRow($tbody, node));
        updateLevelHeader();
        highlightSelectedRow();
    }

    function renderRootLevel(response) {
        const $tbody = $(config.tableBodySelector).empty();
        const items = response.Items ?? response.items ?? [];
        const total = response.TotalCount ?? response.totalCount ?? 0;
        const totalPages = Math.max(1, Math.ceil(total / state.pageSize));

        $(config.emptySelector).toggleClass('d-none', items.length > 0);
        $(config.pageInfoSelector).text(`Page ${state.page} of ${totalPages} (${total} items)`);
        $(config.prevSelector).prop('disabled', state.page <= 1);
        $(config.nextSelector).prop('disabled', state.page >= totalPages);

        if (config.emptyMessageSelector) {
            $(config.emptyMessageSelector).text('No nodes at this level.');
        }

        updateLevelHeader();
        items.forEach(node => appendNodeRow($tbody, node, 0));
        restoreExpandedNodes($tbody);
        highlightSelectedRow();
    }

    function highlightSelectedRow() {
        $(config.tableBodySelector).find('.module-tree-row').removeClass('module-tree-row--selected');
        if (!state.selected || state.selected.isNew) return;

        $(config.tableBodySelector).find('.module-tree-row').each(function () {
            const $row = $(this);
            if (parseInt($row.data('level'), 10) === state.selected.levelIndex
                && parseInt($row.data('id'), 10) === state.selected.id) {
                $row.addClass('module-tree-row--selected');
            }
        });
    }

    function restoreExpandedNodes($tbody) {
        $tbody.find('.module-tree-expand').each(function () {
            const $row = $(this).closest('tr');
            const expandKey = $row.data('expand-key');
            if (state.expanded.get(expandKey) !== true) return;

            const levelIndex = parseInt($row.data('level'), 10);
            const id = parseInt($row.data('id'), 10);
            const $childBlock = $row.next('.module-tree-child-block');
            if ($childBlock.length && !state.expanded.has(`${expandKey}:data`)) {
                loadChildren(expandKey, levelIndex, id, $childBlock.find('tbody'));
            }
        });
    }

    function loadData() {
        if (state.loading) return;

        state.searchMode = !!state.searchTerm;
        updateSearchUi();
        updateSortIcons();
        setLoading(true);

        if (state.searchMode) {
            if (state.searchLevel === -1) {
                const requests = levels.map(level => {
                    const idx = level.LevelIndex ?? level.levelIndex ?? 0;
                    return fetchLevel(idx, null, 1, state.searchTerm);
                });

                Promise.all(requests)
                    .then(function (responses) {
                        renderSearchResults(responses);
                    })
                    .catch(function (xhr) {
                        const err = xhr?.responseJSON?.error ?? xhr?.statusText ?? 'Search failed.';
                        notify(err, 'danger');
                    })
                    .finally(finishLoad);
                return;
            }

            fetchLevel(state.searchLevel, null, state.page, state.searchTerm)
                .done(function (response) {
                    const $tbody = $(config.tableBodySelector).empty();
                    updateLevelHeader();
                    renderPagedResult($tbody, response, appendSearchRow);
                    highlightSelectedRow();
                })
                .fail(function (xhr) {
                    notify(xhr.responseJSON?.error ?? 'Search failed.', 'danger');
                })
                .always(finishLoad);
            return;
        }

        fetchLevel(0, null, state.page)
            .done(renderRootLevel)
            .fail(function (xhr) {
                notify(xhr.responseJSON?.error ?? 'Failed to load tree.', 'danger');
            })
            .always(finishLoad);
    }

    function finishLoad() {
        setLoading(false);
        if (typeof MetaForgeProgress !== 'undefined') {
            MetaForgeProgress.finishPageLoad();
        }
    }

    function loadChildren(expandKey, levelIndex, parentId, $childTbody) {
        fetchLevel(levelIndex + 1, parentId, 1)
            .done(function (response) {
                const items = response.Items ?? response.items ?? [];
                state.expanded.set(`${expandKey}:data`, items);
                $childTbody.empty();
                items.forEach(node => appendNodeRow($childTbody, node, levelIndex + 1, expandKey));
            })
            .fail(function (xhr) {
                notify(xhr.responseJSON?.error ?? 'Failed to load children.', 'danger');
            });
    }

    function loadFormForNode(levelIndex, entityName, id, isNew, parentId) {
        const level = getLevel(levelIndex);
        if (!level || !entityName) return;

        const formDef = level.Form ?? level.form;
        if (!formDef) return;

        $(config.formPlaceholderSelector).addClass('d-none');
        $(config.formSelector).closest('.module-tree-form-shell').removeClass('d-none');
        $(config.saveSelector).prop('disabled', false);
        $(config.clearSelector).prop('disabled', false);
        $(config.deleteSelector).prop('disabled', isNew || !canDeleteEntity(entityName));

        const formName = formDef.FormName ?? formDef.formName ?? entityName;
        const title = isNew ? `New ${formName}` : `Edit ${formName}`;
        $(config.formTitleSelector).text(title);
        $(config.formSubtitleSelector).text(`${entityName} · Level ${levelIndex + 1}`);

        $(config.formSelector).attr('data-entity', entityName).attr('data-module', config.formCode);
        DynamicForm.init(config.formSelector, formDef, { layoutClass: 'master-detail-form' });

        if (isNew) {
            if (!canCreateEntity(entityName)) {
                notify('You do not have permission to create this record.', 'warning');
                $(config.clearSelector).trigger('click');
                return;
            }
            DynamicForm.showNew();
            const fk = level.ForeignKey ?? level.foreignKey;
            if (parentId && fk) {
                const data = {};
                data[fk] = parentId;
                DynamicForm.setDataWhenReady(data);
            } else {
                DynamicForm.refreshLookups();
            }
            state.selected = { levelIndex, entityName, id: null, isNew: true, parentId };
        } else {
            if (!canOpenEntity(entityName)) {
                notify('You do not have permission to view this record.', 'warning');
                $(config.clearSelector).trigger('click');
                return;
            }

            $.get(`/api/metaforge/crud/${entityName}/${id}`)
                .done(function (data) {
                    DynamicForm.setDataWhenReady(data);
                    state.selected = { levelIndex, entityName, id, isNew: false, parentId: null };
                    highlightSelectedRow();
                })
                .fail(function (xhr) {
                    notify(xhr.responseJSON?.error ?? 'Failed to load record.', 'danger');
                });
        }

        const readOnly = isNew ? !canCreateEntity(entityName) : !canEditEntity(entityName);
        $(config.formSelector + ' :input').prop('disabled', readOnly);
        $(config.saveSelector).toggleClass('d-none', readOnly);
        if (config.deleteSelector) {
            $(config.deleteSelector).toggleClass('d-none', !canDeleteEntity(entityName));
        }
    }

    function saveSelected() {
        if (!state.selected) return;

        const { entityName, isNew } = state.selected;
        if (isNew ? !canCreateEntity(entityName) : !canEditEntity(entityName)) {
            notify('You do not have permission to save this record.', 'warning');
            return;
        }

        DynamicForm.save()
            .then(function () {
                notify('Saved successfully.', 'success');
                state.expanded.clear();
                loadData();
                $(config.clearSelector).trigger('click');
            })
            .catch(function (err) {
                notify(err?.message ?? 'Save failed.', 'danger');
            });
    }

    function deleteSelected() {
        if (!state.selected || state.selected.isNew) return;
        const { entityName, id } = state.selected;

        if (!canDeleteEntity(entityName)) {
            notify('You do not have permission to delete this record.', 'warning');
            return;
        }

        if (!window.confirm('Delete this record?')) return;

        $.ajax({ url: `/api/metaforge/crud/${entityName}/${id}`, method: 'DELETE' })
            .done(function () {
                notify('Deleted successfully.', 'success');
                state.expanded.clear();
                loadData();
                $(config.clearSelector).trigger('click');
            })
            .fail(function (xhr) {
                notify(xhr.responseJSON?.error ?? 'Delete failed.', 'danger');
            });
    }

    function resetSearch() {
        state.searchTerm = '';
        state.searchLevel = parseInt($(config.searchLevelSelector).val(), 10);
        if (Number.isNaN(state.searchLevel)) state.searchLevel = -1;
        state.page = 1;
        state.expanded.clear();
        $(config.searchSelector).val('');
        updateSearchUi();
        loadData();
    }

    function bindEvents() {
        let searchTimer;

        $(config.searchSelector).on('input', function () {
            clearTimeout(searchTimer);
            searchTimer = setTimeout(function () {
                state.searchTerm = $(config.searchSelector).val()?.trim() || '';
                state.page = 1;
                state.expanded.clear();
                updateSearchUi();
                loadData();
            }, 300);
        });

        if (config.searchClearSelector) {
            $(config.searchClearSelector).on('click', resetSearch);
        }

        if (config.searchLevelSelector) {
            $(config.searchLevelSelector).on('change', function () {
                state.searchLevel = parseInt($(this).val(), 10);
                if (Number.isNaN(state.searchLevel)) state.searchLevel = -1;
                state.page = 1;
                state.expanded.clear();
                if (state.searchTerm) {
                    loadData();
                } else {
                    updateLevelHeader();
                }
            });
        }

        $(config.pageSizeSelector).on('change', function () {
            state.pageSize = parseInt($(this).val(), 10) || 25;
            state.page = 1;
            state.expanded.clear();
            loadData();
        });

        $(config.prevSelector).on('click', function () {
            if (state.page > 1) {
                state.page--;
                loadData();
            }
        });

        $(config.nextSelector).on('click', function () {
            state.page++;
            loadData();
        });

        if (config.collapseAllSelector) {
            $(config.collapseAllSelector).on('click', function () {
                state.expanded.clear();
                if (!state.searchMode) {
                    loadData();
                }
            });
        }

        $('#multiTreeGrid').on('click', '.module-tree-sort', function () {
            const col = $(this).data('sort');
            if (!col) return;

            if (state.sortColumn && state.sortColumn.toLowerCase() === col.toLowerCase()) {
                state.sortDescending = !state.sortDescending;
            } else {
                state.sortColumn = col;
                state.sortDescending = false;
            }

            state.page = 1;
            loadData();
        });

        $(config.tableBodySelector).on('click', '.module-tree-expand', function (e) {
            e.stopPropagation();
            const $row = $(this).closest('tr');
            const expandKey = $row.data('expand-key');
            const levelIndex = parseInt($row.data('level'), 10);
            const id = parseInt($row.data('id'), 10);
            const isExpanded = state.expanded.get(expandKey) === true;

            if (isExpanded) {
                state.expanded.delete(expandKey);
                state.expanded.delete(`${expandKey}:data`);
            } else {
                state.expanded.set(expandKey, true);
            }

            loadData();
            if (!isExpanded) {
                setTimeout(function () {
                    const $targetRow = $(`${config.tableBodySelector} tr[data-expand-key="${expandKey}"]`);
                    const $childBlock = $targetRow.next('.module-tree-child-block');
                    if ($childBlock.length) {
                        loadChildren(expandKey, levelIndex, id, $childBlock.find('tbody'));
                    }
                }, 0);
            }
        });

        $(config.tableBodySelector).on('click', '.module-tree-select', function () {
            const $row = $(this).closest('tr');
            loadFormForNode(
                parseInt($row.data('level'), 10),
                $row.data('entity'),
                parseInt($row.data('id'), 10),
                false,
                null);
        });

        $(config.tableBodySelector).on('click', '.module-tree-add-child', function (e) {
            e.stopPropagation();
            const $row = $(this).closest('tr');
            const parentLevel = parseInt($row.data('level'), 10);
            const childLevel = getLevel(parentLevel + 1);
            loadFormForNode(
                parentLevel + 1,
                childLevel?.EntityName ?? childLevel?.entityName,
                null,
                true,
                parseInt($row.data('id'), 10));
        });

        $(config.addRootSelector).on('click', function () {
            const root = getLevel(0);
            loadFormForNode(0, root?.EntityName ?? root?.entityName, null, true, null);
        });

        $(config.saveSelector).on('click', saveSelected);
        $(config.deleteSelector).on('click', deleteSelected);
        $(config.clearSelector).on('click', function () {
            state.selected = null;
            $(config.formPlaceholderSelector).removeClass('d-none');
            $(config.formSelector).closest('.module-tree-form-shell').addClass('d-none');
            $(config.saveSelector).prop('disabled', true);
            $(config.clearSelector).prop('disabled', true);
            $(config.deleteSelector).prop('disabled', true);
            $(config.formTitleSelector).text('Select a node');
            $(config.formSubtitleSelector).text('Click a tree node to view or edit its details.');
            $(config.formSelector).empty();
            highlightSelectedRow();
        });
    }

    function init(options) {
        config = options || {};
        permissions = config.permissions || {};
        levelPermissions = config.levelPermissions || {};
        levels = config.treeScreen?.Levels ?? config.treeScreen?.levels ?? [];
        state.pageSize = parseInt($(config.pageSizeSelector).val(), 10) || 25;
        state.searchLevel = parseInt($(config.searchLevelSelector).val(), 10);
        if (Number.isNaN(state.searchLevel)) state.searchLevel = -1;

        if (!levels.length) {
            notify('Tree screen has no levels configured.', 'warning');
            return;
        }

        buildTableHeader();
        bindEvents();
        updateSearchUi();
        updateSortIcons();
        loadData();
    }

    return { init };
})();
