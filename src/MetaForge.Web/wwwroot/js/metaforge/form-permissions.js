/**
 * Shared form permission helpers — supports PascalCase/camelCase DTOs and GrantedActions.
 */
const MetaForgePermissions = (function () {
    const actionProps = {
        View: ['CanView', 'canView'],
        Create: ['CanCreate', 'canCreate'],
        Edit: ['CanEdit', 'canEdit'],
        Delete: ['CanDelete', 'canDelete'],
        Export: ['CanExport', 'canExport'],
        Approve: ['CanApprove', 'canApprove']
    };

    function grantedActions(perms) {
        const granted = perms?.GrantedActions ?? perms?.grantedActions;
        return Array.isArray(granted) ? granted : [];
    }

    function can(perms, action) {
        if (!perms || !action) return false;

        const normalized = String(action);
        if (grantedActions(perms).some(a => String(a).toLowerCase() === normalized.toLowerCase())) {
            return true;
        }

        const props = actionProps[normalized];
        if (props) {
            return props.some(p => perms[p] === true);
        }

        return false;
    }

    function canView(perms) { return can(perms, 'View'); }
    function canCreate(perms) { return can(perms, 'Create'); }
    function canEdit(perms) { return can(perms, 'Edit'); }
    function canDelete(perms) { return can(perms, 'Delete'); }
    function canExport(perms) { return can(perms, 'Export'); }
    function canApprove(perms) { return can(perms, 'Approve'); }
    function canModify(perms) { return canCreate(perms) || canEdit(perms); }
    function canSaveMaster(perms, isNew) { return isNew ? canCreate(perms) : canEdit(perms); }

    function createApi(perms) {
        return {
            can: (action) => can(perms, action),
            canView: () => canView(perms),
            canCreate: () => canCreate(perms),
            canEdit: () => canEdit(perms),
            canDelete: () => canDelete(perms),
            canExport: () => canExport(perms),
            canApprove: () => canApprove(perms),
            canModify: () => canModify(perms),
            canSaveMaster: (isNew) => canSaveMaster(perms, isNew)
        };
    }

    return {
        can,
        canView,
        canCreate,
        canEdit,
        canDelete,
        canExport,
        canApprove,
        canModify,
        canSaveMaster,
        createApi
    };
})();
