(function () {
    const modal = new bootstrap.Modal(document.getElementById('policyModal'));

    function resetForm() {
        document.getElementById('policyId').value = '0';
        document.getElementById('policyForm').reset();
        document.getElementById('policyMaxAttempts').value = '5';
        document.getElementById('policyBaseDelay').value = '60';
        document.getElementById('policyMaxDelay').value = '3600';
        document.getElementById('policyMultiplier').value = '2';
        document.getElementById('policyJitter').checked = true;
        document.getElementById('policyActive').checked = true;
    }

    async function loadPolicy(id) {
        const res = await fetch(`/api/metaforge/emailconfig/retry-policies/${id}`);
        if (!res.ok) return alert('Failed to load policy');
        const p = await res.json();
        document.getElementById('policyId').value = p.Id;
        document.getElementById('policyCode').value = p.Code;
        document.getElementById('policyName').value = p.Name;
        document.getElementById('policyMaxAttempts').value = p.MaxAttempts;
        document.getElementById('policyStrategy').value = p.BackoffStrategy;
        document.getElementById('policyBaseDelay').value = p.BaseDelaySeconds;
        document.getElementById('policyMaxDelay').value = p.MaxDelaySeconds;
        document.getElementById('policyMultiplier').value = p.BackoffMultiplier;
        document.getElementById('policyJitter').checked = p.UseJitter;
        document.getElementById('policyDefault').checked = p.IsDefault;
        document.getElementById('policyActive').checked = p.IsActive;
        modal.show();
    }

    async function savePolicy() {
        const dto = {
            Id: parseInt(document.getElementById('policyId').value) || 0,
            Code: document.getElementById('policyCode').value.trim(),
            Name: document.getElementById('policyName').value.trim(),
            MaxAttempts: parseInt(document.getElementById('policyMaxAttempts').value) || 5,
            BackoffStrategy: document.getElementById('policyStrategy').value,
            BaseDelaySeconds: parseInt(document.getElementById('policyBaseDelay').value) || 60,
            MaxDelaySeconds: parseInt(document.getElementById('policyMaxDelay').value) || 3600,
            BackoffMultiplier: parseFloat(document.getElementById('policyMultiplier').value) || 2,
            UseJitter: document.getElementById('policyJitter').checked,
            IsDefault: document.getElementById('policyDefault').checked,
            IsActive: document.getElementById('policyActive').checked
        };

        const res = await fetch('/api/metaforge/emailconfig/retry-policies', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(dto)
        });

        if (!res.ok) {
            const err = await res.json().catch(() => ({}));
            alert(err.title || err.error || 'Save failed');
            return;
        }

        location.reload();
    }

    document.getElementById('btnNewPolicy')?.addEventListener('click', () => { resetForm(); modal.show(); });
    document.getElementById('btnSavePolicy')?.addEventListener('click', savePolicy);

    document.getElementById('policiesTable')?.addEventListener('click', async (e) => {
        const row = e.target.closest('tr');
        if (!row) return;
        const id = row.dataset.id;
        if (e.target.classList.contains('btn-edit')) loadPolicy(id);
        if (e.target.classList.contains('btn-delete')) {
            if (!confirm('Delete this policy?')) return;
            const res = await fetch(`/api/metaforge/emailconfig/retry-policies/${id}`, { method: 'DELETE' });
            if (!res.ok) alert('Delete failed');
            else location.reload();
        }
    });
})();
