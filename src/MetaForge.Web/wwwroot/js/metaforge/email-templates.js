(function () {
    const cfg = window.emailTemplateConfig || { forms: [], bindings: [], triggerEvents: [] };
    let bindings = [...(cfg.bindings || [])];

    function renderBindings() {
        const container = document.getElementById('bindingsContainer');
        if (!container) return;
        container.innerHTML = '';

        bindings.forEach((b, index) => {
            const div = document.createElement('div');
            div.className = 'border rounded p-3 mb-2';
            div.innerHTML = `
                <div class="row g-2 align-items-end">
                    <div class="col-md-3">
                        <label class="form-label small">Feature Form</label>
                        <select class="form-select form-select-sm binding-form" data-index="${index}">
                            ${cfg.forms.map(f => `<option value="${f.Id}" ${f.Id === b.FormId ? 'selected' : ''}>${f.Name}</option>`).join('')}
                        </select>
                    </div>
                    <div class="col-md-2">
                        <label class="form-label small">Trigger</label>
                        <select class="form-select form-select-sm binding-trigger" data-index="${index}">
                            ${cfg.triggerEvents.map(t => `<option value="${t}" ${t === b.TriggerEvent ? 'selected' : ''}>${t}</option>`).join('')}
                        </select>
                    </div>
                    <div class="col-md-2">
                        <label class="form-label small">Action Code</label>
                        <input type="text" class="form-control form-control-sm binding-action" data-index="${index}" value="${b.ActionCode || ''}" />
                    </div>
                    <div class="col-md-2">
                        <label class="form-label small">Recipient Field</label>
                        <input type="text" class="form-control form-control-sm binding-recipient" data-index="${index}" value="${b.RecipientField || ''}" placeholder="Email" />
                    </div>
                    <div class="col-md-2">
                        <label class="form-label small">Condition</label>
                        <input type="text" class="form-control form-control-sm binding-condition" data-index="${index}" value="${b.ConditionExpression || ''}" placeholder="Status=Active" />
                    </div>
                    <div class="col-md-1">
                        <button type="button" class="btn btn-sm btn-outline-danger btn-remove-binding" data-index="${index}"><i class="fa-solid fa-trash"></i></button>
                    </div>
                </div>`;
            container.appendChild(div);
        });

        container.querySelectorAll('.binding-form').forEach(el => el.addEventListener('change', syncBindingsFromDom));
        container.querySelectorAll('.binding-trigger').forEach(el => el.addEventListener('change', syncBindingsFromDom));
        container.querySelectorAll('.binding-action').forEach(el => el.addEventListener('input', syncBindingsFromDom));
        container.querySelectorAll('.binding-recipient').forEach(el => el.addEventListener('input', syncBindingsFromDom));
        container.querySelectorAll('.binding-condition').forEach(el => el.addEventListener('input', syncBindingsFromDom));
        container.querySelectorAll('.btn-remove-binding').forEach(el => el.addEventListener('click', (e) => {
            const index = parseInt(e.currentTarget.dataset.index);
            bindings.splice(index, 1);
            renderBindings();
        }));
    }

    function syncBindingsFromDom() {
        const container = document.getElementById('bindingsContainer');
        bindings = [...container.querySelectorAll('.border.rounded')].map((row, i) => ({
            Id: bindings[i]?.Id || 0,
            FormId: parseInt(row.querySelector('.binding-form').value),
            TriggerEvent: row.querySelector('.binding-trigger').value,
            ActionCode: row.querySelector('.binding-action').value.trim() || null,
            RecipientField: row.querySelector('.binding-recipient').value.trim() || null,
            ConditionExpression: row.querySelector('.binding-condition').value.trim() || null,
            IsActive: true
        }));
    }

    async function saveTemplate() {
        syncBindingsFromDom();
        const channelVal = document.getElementById('templateChannel').value;
        const policyVal = document.getElementById('templatePolicy').value;

        const dto = {
            Id: parseInt(document.getElementById('templateId').value) || 0,
            Code: document.getElementById('templateCode').value.trim(),
            Name: document.getElementById('templateName').value.trim(),
            Subject: document.getElementById('templateSubject').value.trim(),
            BodyHtml: document.getElementById('templateBody').value,
            BodyText: document.getElementById('templateBodyText').value.trim() || null,
            DefaultToExpression: document.getElementById('templateTo').value.trim() || null,
            EmailChannelId: channelVal ? parseInt(channelVal) : null,
            RetryPolicyId: policyVal ? parseInt(policyVal) : null,
            Culture: 'en',
            IsActive: document.getElementById('templateActive').checked,
            Bindings: bindings
        };

        const res = await fetch('/api/metaforge/emailconfig/templates', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(dto)
        });

        if (!res.ok) {
            const err = await res.json().catch(() => ({}));
            alert(err.title || err.error || 'Save failed');
            return;
        }

        const data = await res.json();
        window.location.href = data.url || '/EmailAdmin/Templates';
    }

    document.getElementById('btnAddBinding')?.addEventListener('click', () => {
        if (cfg.forms.length === 0) return alert('No feature forms available.');
        bindings.push({
            Id: 0,
            FormId: cfg.forms[0].Id,
            TriggerEvent: 'OnCreate',
            IsActive: true
        });
        renderBindings();
    });

    document.getElementById('btnSaveTemplate')?.addEventListener('click', saveTemplate);
    renderBindings();
})();
