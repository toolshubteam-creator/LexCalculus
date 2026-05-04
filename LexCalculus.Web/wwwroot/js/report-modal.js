// Şikayet modal handler — paylaşımlı (Faz 4.10 P1, Faz 5.7 mesajlar.js reuse).
// Sayfada _ReportModal partial render edilmiş ve [data-report-target-type]
// trigger element'leri varsa otomatik bağlanır. Faz 5.7'de makale-page.js'ten
// extract edildi (DRY — makale + mesajlar sayfalarında tek kaynak).
(function () {
    'use strict';

    function getCsrfToken() {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    async function postJson(url, body) {
        const init = {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-CSRF-TOKEN': getCsrfToken()
            }
        };
        if (body !== undefined) init.body = JSON.stringify(body);

        const response = await fetch(url, init);
        let data = {};
        try { data = await response.json(); } catch (_) { /* boş body */ }

        if (response.status === 429) {
            const retryAfter = response.headers.get('Retry-After') || '60';
            const message = (data && data.error)
                ? data.error
                : 'Çok fazla istek. ' + retryAfter + ' saniye sonra tekrar deneyin.';
            alert(message);
        }

        return { ok: response.ok, status: response.status, data };
    }

    const reportModal = document.getElementById('report-modal');
    const reportForm = document.getElementById('report-form');
    if (!reportModal || !reportForm) return;

    const reasonSelect = document.getElementById('report-reason');
    const noteTextarea = document.getElementById('report-note');
    const noteCounter = document.getElementById('report-note-counter');
    const noteRequired = document.getElementById('report-note-required');
    const errorEl = document.getElementById('report-error');
    const targetTypeInput = document.getElementById('report-target-type');
    const targetIdInput = document.getElementById('report-target-id');

    function resetForm() {
        reasonSelect.value = '';
        noteTextarea.value = '';
        noteCounter.textContent = '0 / 500';
        if (noteRequired) noteRequired.hidden = true;
        errorEl.hidden = true;
        errorEl.textContent = '';
    }

    document.addEventListener('click', function (e) {
        const trigger = e.target.closest('[data-report-target-type][data-report-target-id]');
        if (trigger) {
            e.preventDefault();
            resetForm();
            targetTypeInput.value = trigger.dataset.reportTargetType;
            targetIdInput.value = trigger.dataset.reportTargetId;
            if (typeof reportModal.showModal === 'function') {
                reportModal.showModal();
            } else {
                reportModal.setAttribute('open', '');
            }
            return;
        }
        if (e.target.matches('[data-report-cancel]')) {
            if (typeof reportModal.close === 'function') {
                reportModal.close();
            } else {
                reportModal.removeAttribute('open');
            }
        }
    });

    reasonSelect.addEventListener('change', function () {
        if (noteRequired) noteRequired.hidden = reasonSelect.value !== '99';
    });

    noteTextarea.addEventListener('input', function () {
        noteCounter.textContent = noteTextarea.value.length + ' / 500';
    });

    reportForm.addEventListener('submit', async function (e) {
        e.preventDefault();
        errorEl.hidden = true;
        errorEl.textContent = '';

        const reason = parseInt(reasonSelect.value, 10);
        const note = (noteTextarea.value || '').trim();
        const targetType = parseInt(targetTypeInput.value, 10);
        const targetId = parseInt(targetIdInput.value, 10);

        if (!reason) {
            errorEl.textContent = 'Lütfen bir sebep seçin.';
            errorEl.hidden = false;
            return;
        }
        if (reason === 99 && note.length < 10) {
            errorEl.textContent = '"Diğer" sebebinde en az 10 karakter açıklama girin.';
            errorEl.hidden = false;
            return;
        }
        if (!targetType || !targetId) {
            errorEl.textContent = 'Hedef bilgisi eksik.';
            errorEl.hidden = false;
            return;
        }

        const submitBtn = reportForm.querySelector('button[type="submit"]');
        submitBtn.disabled = true;

        const result = await postJson('/api/content-reports/create', {
            targetType: targetType,
            targetId: targetId,
            reason: reason,
            note: note || null
        });

        if (!result.ok) {
            if (result.status !== 429) {
                errorEl.textContent = (result.data && result.data.error) || 'Şikayet gönderilemedi.';
                errorEl.hidden = false;
            }
            submitBtn.disabled = false;
            return;
        }

        if (typeof reportModal.close === 'function') {
            reportModal.close();
        } else {
            reportModal.removeAttribute('open');
        }
        submitBtn.disabled = false;
        alert('Şikayetiniz alındı. İncelendiğinde size bildirim gönderilecek.');
    });
})();
