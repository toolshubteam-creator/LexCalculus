/**
 * /mesajlar/{id} ve /mesajlar/yeni sayfalarının AJAX davranışı.
 * - Detail: send + delete + SignalR real-time + 30sn polling fallback
 * - Yeni: ilk mesaj submit → /api/messages/send → redirect /mesajlar/{convId}
 * Faz 5.5 (polling), Faz 5.6 (SignalR primary, polling fallback).
 *
 * SignalR: window.signalR globalı yüklü ve Hub bağlantısı kurulduysa
 * MessageReceived/MessageDeleted event'leriyle anlık güncelleme yapılır;
 * connection kopuk veya kütüphane yoksa polling devreye girer.
 */
(function () {
    'use strict';

    const POLLING_INTERVAL_MS = 30000;

    function getCsrfToken() {
        const el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    async function postJson(url, body) {
        const token = getCsrfToken();
        const init = {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token,
                'X-CSRF-TOKEN': token
            }
        };
        if (body !== null && body !== undefined) {
            init.body = JSON.stringify(body);
        }
        const response = await fetch(url, init);
        let data = {};
        try { data = await response.json(); } catch (_) { /* boş cevap */ }
        return { ok: response.ok, status: response.status, data };
    }

    // ───────────────── Yeni mesaj sayfası ─────────────────
    const yeniForm = document.querySelector('[data-mesaj-yeni-form]');
    if (yeniForm) {
        const recipientId = parseInt(yeniForm.dataset.recipientId, 10);
        const bodyEl = yeniForm.querySelector('[data-mesaj-yeni-body]');
        const feedback = yeniForm.querySelector('[data-mesaj-yeni-feedback]');
        const submitBtn = yeniForm.querySelector('button[type="submit"]');

        yeniForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            feedback.textContent = '';
            feedback.classList.remove('mesajlar-yeni__feedback--error');
            const body = bodyEl.value.trim();
            if (!body) return;

            submitBtn.disabled = true;
            const result = await postJson('/api/messages/send', { recipientId, body });
            submitBtn.disabled = false;

            if (result.status === 429) {
                feedback.textContent = 'Çok fazla mesaj. Birkaç saniye sonra deneyin.';
                feedback.classList.add('mesajlar-yeni__feedback--error');
                return;
            }
            if (!result.ok) {
                feedback.textContent = (result.data && result.data.error) || 'Mesaj gönderilemedi.';
                feedback.classList.add('mesajlar-yeni__feedback--error');
                return;
            }
            window.location.href = `/mesajlar/${result.data.conversationId}`;
        });
    }

    // ───────────────── Detail sayfası ─────────────────
    const detail = document.querySelector('.mesajlar-detail');
    if (!detail) return;

    const conversationId = parseInt(detail.dataset.conversationId, 10);
    const messagesContainer = detail.querySelector('[data-mesaj-listesi]');
    const form = detail.querySelector('[data-mesaj-form]');
    const bodyTextarea = form ? form.querySelector('[data-mesaj-body]') : null;
    const charCount = detail.querySelector('[data-mesaj-char-count]');
    const loadMoreBtn = detail.querySelector('[data-mesaj-load-more]');

    let lastSeenAt = new Date().toISOString();
    let pollingTimer = null;
    let signalRActive = false;

    function scrollToBottom() {
        if (!messagesContainer) return;
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    function applyDeletePlaceholder(messageId) {
        const card = messagesContainer.querySelector(`[data-message-id="${messageId}"]`);
        if (!card) return;
        const body = card.querySelector('[data-mesaj-body]');
        if (body) {
            const placeholder = document.createElement('p');
            placeholder.className = 'mesaj__deleted';
            placeholder.textContent = '(Bu mesaj silindi)';
            body.replaceWith(placeholder);
        }
        const btn = card.querySelector('[data-mesaj-delete]');
        if (btn) btn.remove();
    }

    function updateCharCount() {
        if (!bodyTextarea || !charCount) return;
        const max = bodyTextarea.maxLength || 1000;
        charCount.textContent = `${bodyTextarea.value.length} / ${max}`;
    }

    // İlk yüklemede en alta
    scrollToBottom();
    updateCharCount();

    // Send
    if (form && bodyTextarea) {
        form.addEventListener('submit', async (e) => {
            e.preventDefault();
            const body = bodyTextarea.value.trim();
            if (!body) return;

            const recipientId = parseInt(detail.dataset.recipientId, 10);
            const submitBtn = form.querySelector('button[type="submit"]');
            submitBtn.disabled = true;

            const result = await postJson('/api/messages/send', { recipientId, body });
            submitBtn.disabled = false;

            if (result.status === 429) {
                alert('Çok fazla mesaj. Biraz bekleyin.');
                return;
            }
            if (!result.ok) {
                alert((result.data && result.data.error) || 'Mesaj gönderilemedi.');
                return;
            }

            messagesContainer.insertAdjacentHTML('beforeend', result.data.html);
            bodyTextarea.value = '';
            updateCharCount();
            scrollToBottom();
            lastSeenAt = new Date().toISOString();
        });

        bodyTextarea.addEventListener('input', updateCharCount);
    }

    // Delete (event delegation)
    if (messagesContainer) {
        messagesContainer.addEventListener('click', async (e) => {
            const btn = e.target.closest('[data-mesaj-delete]');
            if (!btn) return;
            if (!confirm('Bu mesajı silmek istediğinize emin misiniz?')) return;

            const messageId = parseInt(btn.dataset.messageId, 10);
            const result = await postJson(`/api/messages/${messageId}/delete`, null);
            if (!result.ok) {
                alert((result.data && result.data.error) || 'Silinemedi.');
                return;
            }
            applyDeletePlaceholder(messageId);
        });
    }

    // Load more — placeholder (Faz 6+ veya 5.6'da iyileştirilecek)
    if (loadMoreBtn) {
        loadMoreBtn.addEventListener('click', () => {
            alert('Daha fazla yükle henüz aktif değil. Adım 5.6 ile gelecek.');
        });
    }

    // ───────────────── SignalR (primary kanal) ─────────────────
    // window.signalR varsa Hub bağlantısı kurulur. Yüklenememesi/baylantı
    // başarısızlığı sessizdir; UI'da göstergesi yoktur — polling devreye girer.
    if (typeof window.signalR !== 'undefined') {
        try {
            const connection = new window.signalR.HubConnectionBuilder()
                .withUrl('/hubs/messages')
                .withAutomaticReconnect()
                .build();

            connection.on('MessageReceived', async (data) => {
                if (!data || data.conversationId !== conversationId) return;
                messagesContainer.insertAdjacentHTML('beforeend', data.html);
                scrollToBottom();
                lastSeenAt = new Date().toISOString();
                await postJson(`/api/messages/${conversationId}/mark-read`, null)
                    .catch(() => { /* sessiz */ });
            });

            connection.on('MessageDeleted', (data) => {
                if (!data || data.conversationId !== conversationId) return;
                applyDeletePlaceholder(data.messageId);
            });

            connection.onreconnecting(() => { signalRActive = false; });
            connection.onreconnected(() => { signalRActive = true; });
            connection.onclose(() => { signalRActive = false; });

            connection.start()
                .then(() => { signalRActive = true; })
                .catch((err) => {
                    signalRActive = false;
                    console.warn('SignalR start hata, polling fallback:', err);
                });
        } catch (err) {
            signalRActive = false;
            console.warn('SignalR setup hata, polling fallback:', err);
        }
    }

    // ───────────────── Polling fallback ─────────────────
    async function poll() {
        if (signalRActive) return;   // SignalR aktif; polling'i atla
        try {
            const url = `/api/messages/${conversationId}/new?since=${encodeURIComponent(lastSeenAt)}`;
            const response = await fetch(url, { headers: { 'Accept': 'application/json' } });
            if (!response.ok) return;

            const data = await response.json();
            if (data.messages && data.messages.length > 0) {
                data.messages.forEach((html) => {
                    messagesContainer.insertAdjacentHTML('beforeend', html);
                });
                scrollToBottom();
                if (data.latestAt) lastSeenAt = data.latestAt;
                await postJson(`/api/messages/${conversationId}/mark-read`, null);
            }
        } catch (err) {
            console.error('Mesaj polling hatası:', err);
        }
    }

    pollingTimer = setInterval(poll, POLLING_INTERVAL_MS);

    // Sayfa kapatılırken polling temizliği (defansif)
    window.addEventListener('beforeunload', () => {
        if (pollingTimer) clearInterval(pollingTimer);
    });

    // Sayfa açılışında okundu işaretle (server zaten OnGet'te yapıyor; defansif)
    postJson(`/api/messages/${conversationId}/mark-read`, null).catch(() => { /* sessiz */ });
})();
