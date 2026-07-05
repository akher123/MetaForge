(function () {
    const modal = new bootstrap.Modal(document.getElementById('channelModal'));

    function toggleSmtpFields() {
        const isSmtp = document.getElementById('channelProvider').value === 'Smtp';
        document.querySelectorAll('.smtp-field').forEach(el => {
            el.style.display = isSmtp ? '' : 'none';
        });
    }

    function resetForm() {
        document.getElementById('channelId').value = '0';
        document.getElementById('channelForm').reset();
        document.getElementById('channelPort').value = '587';
        document.getElementById('channelActive').checked = true;
        toggleSmtpFields();
    }

    async function loadChannel(id) {
        const res = await fetch(`/api/metaforge/emailconfig/channels/${id}`);
        if (!res.ok) return alert('Failed to load channel');
        const c = await res.json();
        document.getElementById('channelId').value = c.Id;
        document.getElementById('channelCode').value = c.Code;
        document.getElementById('channelName').value = c.Name;
        document.getElementById('channelProvider').value = c.Provider;
        document.getElementById('channelSecret').value = c.CredentialSecretName || '';
        document.getElementById('channelFrom').value = c.FromAddress;
        document.getElementById('channelFromName').value = c.FromDisplayName || '';
        document.getElementById('channelHost').value = c.SmtpHost || '';
        document.getElementById('channelPort').value = c.SmtpPort;
        document.getElementById('channelSsl').value = c.SmtpUseSsl ? 'true' : 'false';
        document.getElementById('channelUsername').value = c.SmtpUsername || '';
        document.getElementById('channelDefault').checked = c.IsDefault;
        document.getElementById('channelActive').checked = c.IsActive;
        toggleSmtpFields();
        modal.show();
    }

    async function saveChannel() {
        const dto = {
            Id: parseInt(document.getElementById('channelId').value) || 0,
            Code: document.getElementById('channelCode').value.trim(),
            Name: document.getElementById('channelName').value.trim(),
            Provider: document.getElementById('channelProvider').value,
            CredentialSecretName: document.getElementById('channelSecret').value.trim() || null,
            FromAddress: document.getElementById('channelFrom').value.trim(),
            FromDisplayName: document.getElementById('channelFromName').value.trim() || null,
            SmtpHost: document.getElementById('channelHost').value.trim() || null,
            SmtpPort: parseInt(document.getElementById('channelPort').value) || 587,
            SmtpUseSsl: document.getElementById('channelSsl').value === 'true',
            SmtpUsername: document.getElementById('channelUsername').value.trim() || null,
            MaxDegreeOfParallelism: 1,
            IsDefault: document.getElementById('channelDefault').checked,
            IsActive: document.getElementById('channelActive').checked
        };

        const res = await fetch('/api/metaforge/emailconfig/channels', {
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

    document.getElementById('btnNewChannel')?.addEventListener('click', () => { resetForm(); modal.show(); });
    document.getElementById('btnSaveChannel')?.addEventListener('click', saveChannel);
    document.getElementById('channelProvider')?.addEventListener('change', toggleSmtpFields);

    document.getElementById('channelsTable')?.addEventListener('click', async (e) => {
        const row = e.target.closest('tr');
        if (!row) return;
        const id = row.dataset.id;
        if (e.target.classList.contains('btn-edit')) loadChannel(id);
        if (e.target.classList.contains('btn-delete')) {
            if (!confirm('Delete this channel?')) return;
            const res = await fetch(`/api/metaforge/emailconfig/channels/${id}`, { method: 'DELETE' });
            if (!res.ok) alert('Delete failed');
            else location.reload();
        }
    });

    toggleSmtpFields();
})();
