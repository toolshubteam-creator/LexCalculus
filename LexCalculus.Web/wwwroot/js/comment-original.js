// Faz 6.8 (#21) — düzenlenmiş yorumun saklı orijinalini göster/gizle.
// Lazy fetch: ilk açılışta /api/post-comments/{id}/original'dan çekilir,
// sonraki tıklamalar yalnızca toggle. Event delegation → AJAX ile sonradan
// eklenen/düzenlenen yorumlar da çalışır.
(function () {
    'use strict';

    function fmtDate(iso) {
        if (!iso) return '';
        const d = new Date(iso);
        if (isNaN(d.getTime())) return '';
        return d.toLocaleString('tr-TR', {
            day: 'numeric', month: 'long', year: 'numeric',
            hour: '2-digit', minute: '2-digit'
        });
    }

    async function loadOriginal(commentId, container) {
        const bodyEl = container.querySelector('[data-yorum-original-body]');
        const metaEl = container.querySelector('[data-yorum-original-meta]');
        if (!bodyEl) return;

        try {
            const res = await fetch('/api/post-comments/' + commentId + '/original', {
                headers: { 'Accept': 'application/json' }
            });
            if (!res.ok) {
                bodyEl.textContent = 'Orijinal yorum yüklenemedi.';
                container.dataset.loaded = 'error';
                return;
            }
            const data = await res.json();
            // originalBody sunucuda sanitize edilmiş HTML (whitelist <a>, <br>) —
            // ana gövde ile aynı güven seviyesi, innerHTML güvenli.
            bodyEl.innerHTML = data.originalBody || '';
            if (metaEl) {
                const created = fmtDate(data.originalCreatedAt);
                metaEl.textContent = created
                    ? 'Orijinal yorum (' + created + '):'
                    : 'Orijinal yorum:';
            }
            container.dataset.loaded = 'true';
        } catch (_) {
            bodyEl.textContent = 'Orijinal yorum yüklenemedi.';
            container.dataset.loaded = 'error';
        }
    }

    document.addEventListener('click', function (e) {
        const toggle = e.target.closest('[data-yorum-original]');
        if (!toggle) return;
        e.preventDefault();

        const commentId = toggle.dataset.yorumOriginal;
        const container = document.getElementById('yorum-original-' + commentId);
        if (!container) return;

        const isHidden = container.hasAttribute('hidden');
        if (isHidden) {
            container.removeAttribute('hidden');
            toggle.setAttribute('aria-expanded', 'true');
            toggle.textContent = '(orijinali gizle)';
            // Lazy fetch — yalnızca daha önce başarıyla yüklenmediyse.
            if (container.dataset.loaded !== 'true') {
                loadOriginal(commentId, container);
            }
        } else {
            container.setAttribute('hidden', '');
            toggle.setAttribute('aria-expanded', 'false');
            toggle.textContent = '(orijinali göster)';
        }
    });
})();
