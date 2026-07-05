(function () {
    document.getElementById('emailLogTable')?.addEventListener('click', async (e) => {
        const row = e.target.closest('tr');
        if (!row) return;
        const id = row.dataset.id;

        if (e.target.classList.contains('btn-cancel')) {
            if (!confirm('Cancel this email?')) return;
            const res = await fetch(`/api/metaforge/email/messages/${id}/cancel`, { method: 'POST' });
            if (!res.ok) alert('Cancel failed');
            else location.reload();
        }

        if (e.target.classList.contains('btn-resend')) {
            if (!confirm('Create a new send attempt for this email?')) return;
            const res = await fetch(`/api/metaforge/email/messages/${id}/resend`, { method: 'POST' });
            if (!res.ok) alert('Resend failed');
            else location.reload();
        }
    });
})();
