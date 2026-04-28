(function () {
    'use strict';

    const POLL_INTERVAL_MS = 60_000;

    function init() {
        const bell = document.querySelector('.notif-bell');
        if (!bell) return;

        const btn = bell.querySelector('.notif-bell__btn');
        const list = bell.querySelector('[data-list]');
        const badge = bell.querySelector('.notif-bell__badge');
        const pollUrl = bell.dataset.pollUrl;

        let dropdownLoaded = false;

        btn.addEventListener('click', async (e) => {
            e.stopPropagation();
            const isOpen = bell.classList.toggle('notif-bell--open');
            btn.setAttribute('aria-expanded', isOpen ? 'true' : 'false');

            if (isOpen && !dropdownLoaded) {
                await loadPreview();
                dropdownLoaded = true;
            }
        });

        document.addEventListener('click', (e) => {
            if (!bell.contains(e.target)) {
                bell.classList.remove('notif-bell--open');
                btn.setAttribute('aria-expanded', 'false');
            }
        });

        async function loadPreview() {
            try {
                const res = await fetch('/bildirimler/onizleme', {
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                if (!res.ok) throw new Error('preview fetch failed');
                const data = await res.json();
                renderPreview(data.items);
            } catch (e) {
                list.innerHTML = '<div class="notif-bell__empty">Yüklenemedi.</div>';
            }
        }

        function renderPreview(items) {
            if (!items || items.length === 0) {
                list.innerHTML = '<div class="notif-bell__empty">Bildirim yok.</div>';
                return;
            }
            const html = items.map(n => {
                const cls = n.isRead ? 'notif-bell__row--read' : 'notif-bell__row--unread';
                const href = n.link || '/bildirimler';
                const time = formatRelativeTime(new Date(n.createdAt));
                return `<a class="notif-bell__row ${cls}" href="${href}">
                    <div class="notif-bell__row-title">${escapeHtml(n.title)}</div>
                    <div class="notif-bell__row-body">${escapeHtml(truncate(n.body, 120))}</div>
                    <div class="notif-bell__row-time">${time}</div>
                </a>`;
            }).join('');
            list.innerHTML = html;
        }

        function escapeHtml(s) {
            const d = document.createElement('div');
            d.textContent = s;
            return d.innerHTML;
        }
        function truncate(s, n) { return s.length > n ? s.slice(0, n) + '…' : s; }

        function formatRelativeTime(date) {
            const diff = (Date.now() - date.getTime()) / 1000;
            if (diff < 60) return 'az önce';
            if (diff < 3600) return Math.floor(diff / 60) + ' dk önce';
            if (diff < 86400) return Math.floor(diff / 3600) + ' saat önce';
            return Math.floor(diff / 86400) + ' gün önce';
        }

        async function pollCount() {
            if (document.hidden) return;
            try {
                const res = await fetch(pollUrl, {
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                if (!res.ok) return;
                const data = await res.json();
                updateBadge(data.unreadCount);
            } catch (e) { /* swallow, retry next tick */ }
        }

        function updateBadge(count) {
            if (count > 0) {
                badge.classList.remove('notif-bell__badge--hidden');
                badge.textContent = count > 99 ? '99+' : String(count);
                if (bell.classList.contains('notif-bell--open')) loadPreview();
            } else {
                badge.classList.add('notif-bell__badge--hidden');
            }
        }

        setInterval(pollCount, POLL_INTERVAL_MS);
        document.addEventListener('visibilitychange', () => {
            if (!document.hidden) pollCount();
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
