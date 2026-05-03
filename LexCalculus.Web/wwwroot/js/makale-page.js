// Faz 4.9 P2 — /uye/{slug}/makale/{slug} sayfası: yorum + beğeni AJAX.
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

        // Faz 5.2 — rate limit: 429 yakalanır, kullanıcıya alert.
        // Çağıran handler erken dönmek için status'u kontrol etmeli.
        if (response.status === 429) {
            const retryAfter = response.headers.get('Retry-After') || '60';
            const message = (data && data.error)
                ? data.error
                : 'Çok fazla istek. ' + retryAfter + ' saniye sonra tekrar deneyin.';
            alert(message);
        }

        return { ok: response.ok, status: response.status, data };
    }

    function updateCommentCount(delta) {
        const countEl = document.querySelector('.yorumlar__count');
        if (!countEl) return;
        const current = parseInt(countEl.textContent, 10) || 0;
        countEl.textContent = Math.max(0, current + delta);
    }

    // ─── Beğeni toggle ───────────────────────────────────────────────────
    const likeButton = document.querySelector('.makale__like:not(.makale__like--anon)');
    if (likeButton && likeButton.tagName === 'BUTTON') {
        likeButton.addEventListener('click', async function () {
            if (likeButton.disabled) return;
            likeButton.disabled = true;

            const postId = parseInt(likeButton.dataset.postId, 10);
            const result = await postJson('/api/post-likes/toggle', { postId });

            if (!result.ok) {
                // 429 zaten postJson içinde alert gösterdi — ikinci alert atma.
                if (result.status !== 429) {
                    alert('Beğeni işlemi başarısız: ' + (result.data.error || 'Bilinmeyen hata'));
                }
                likeButton.disabled = false;
                return;
            }

            const { isLiked, likeCount } = result.data;
            const iconEl = likeButton.querySelector('.makale__like-icon');
            const countEl = likeButton.querySelector('[data-like-count]');

            likeButton.classList.toggle('makale__like--active', isLiked);
            if (iconEl) iconEl.textContent = isLiked ? '❤' : '♡';
            if (countEl) countEl.textContent = likeCount;
            likeButton.setAttribute('aria-pressed', isLiked ? 'true' : 'false');

            likeButton.disabled = false;
        });
    }

    // ─── Yorum form (yeni) ───────────────────────────────────────────────
    const yorumForm = document.getElementById('yorum-form');
    if (yorumForm) {
        const textarea = yorumForm.querySelector('textarea[name="body"]');
        const counter = yorumForm.querySelector('[data-counter]');
        const errorEl = yorumForm.querySelector('[data-error]');
        const submitBtn = yorumForm.querySelector('button[type="submit"]');

        function updateCounter() {
            counter.textContent = (textarea.value || '').length + ' / 1000';
        }
        textarea.addEventListener('input', updateCounter);
        updateCounter();

        yorumForm.addEventListener('submit', async function (e) {
            e.preventDefault();
            errorEl.hidden = true;
            errorEl.textContent = '';

            const postId = parseInt(yorumForm.querySelector('input[name="postId"]').value, 10);
            const body = (textarea.value || '').trim();

            if (!body) {
                errorEl.textContent = 'Yorum boş olamaz.';
                errorEl.hidden = false;
                return;
            }

            submitBtn.disabled = true;

            const result = await postJson('/api/post-comments/create', { postId, body });

            if (!result.ok) {
                // 429 zaten postJson içinde alert gösterdi — form içi hata mesajı atma.
                if (result.status !== 429) {
                    errorEl.textContent = result.data.error || 'Yorum gönderilemedi.';
                    errorEl.hidden = false;
                }
                submitBtn.disabled = false;
                return;
            }

            // Liste oluştur veya append
            let list = document.getElementById('yorum-list');
            if (!list) {
                const emptyEl = document.getElementById('yorum-list-empty');
                if (emptyEl) emptyEl.remove();
                list = document.createElement('ul');
                list.id = 'yorum-list';
                list.className = 'yorum-list';
                yorumForm.parentNode.appendChild(list);
            }
            list.insertAdjacentHTML('beforeend', result.data.html);

            textarea.value = '';
            updateCounter();
            submitBtn.disabled = false;
            updateCommentCount(1);
        });
    }

    // ─── Yorum aksiyonları (event delegation) ────────────────────────────
    function stripHtml(html) {
        const tmp = document.createElement('div');
        tmp.innerHTML = html;
        // <br> → \n dönüşümü ki düzenleme textarea'sında satır sonları korunur
        return (tmp.innerHTML || '')
            .replace(/<br\s*\/?>/gi, '\n')
            .replace(/<[^>]+>/g, '')
            .replace(/&lt;/g, '<')
            .replace(/&gt;/g, '>')
            .replace(/&amp;/g, '&')
            .replace(/&quot;/g, '"')
            .replace(/&#39;/g, "'");
    }

    document.addEventListener('click', async function (e) {
        // Sil
        if (e.target.matches('[data-yorum-delete]')) {
            const li = e.target.closest('.yorum');
            if (!li) return;
            const commentId = li.dataset.commentId;
            if (!confirm('Bu yorumu silmek istediğinizden emin misiniz?')) return;

            const result = await postJson('/api/post-comments/' + commentId + '/delete');
            if (!result.ok) {
                if (result.status !== 429) {
                    alert('Silme başarısız: ' + (result.data.error || 'Bilinmeyen hata'));
                }
                return;
            }
            li.remove();
            updateCommentCount(-1);

            const list = document.getElementById('yorum-list');
            if (list && list.children.length === 0) {
                const empty = document.createElement('p');
                empty.className = 'yorumlar__empty';
                empty.id = 'yorum-list-empty';
                empty.textContent = 'Henüz yorum yok. İlk yorumu siz yapın.';
                list.parentNode.replaceChild(empty, list);
            }
            return;
        }

        // Düzenle (inline form)
        if (e.target.matches('[data-yorum-edit]')) {
            const li = e.target.closest('.yorum');
            if (!li) return;
            const bodyEl = li.querySelector('[data-yorum-body]');
            const actionsEl = li.querySelector('.yorum__actions');
            const currentText = stripHtml(bodyEl.innerHTML);
            bodyEl.dataset.originalHtml = bodyEl.innerHTML;

            bodyEl.innerHTML = '';
            const ta = document.createElement('textarea');
            ta.className = 'yorum-form__textarea';
            ta.maxLength = 1000;
            ta.rows = 3;
            ta.value = currentText;

            const actions = document.createElement('div');
            actions.className = 'yorum__edit-actions';
            actions.innerHTML =
                '<button type="button" class="btn btn--primary" data-yorum-save>Kaydet</button> ' +
                '<button type="button" class="btn btn--ghost" data-yorum-cancel>İptal</button>';

            bodyEl.appendChild(ta);
            bodyEl.appendChild(actions);
            ta.focus();
            if (actionsEl) actionsEl.hidden = true;
            return;
        }

        // Düzenle - Kaydet
        if (e.target.matches('[data-yorum-save]')) {
            const li = e.target.closest('.yorum');
            if (!li) return;
            const bodyEl = li.querySelector('[data-yorum-body]');
            const ta = bodyEl.querySelector('textarea');
            const newBody = (ta.value || '').trim();
            const commentId = li.dataset.commentId;

            if (!newBody) {
                alert('Yorum boş olamaz.');
                return;
            }
            e.target.disabled = true;

            const result = await postJson('/api/post-comments/' + commentId + '/update',
                { body: newBody });
            if (!result.ok) {
                if (result.status !== 429) {
                    alert('Güncelleme başarısız: ' + (result.data.error || 'Bilinmeyen hata'));
                }
                e.target.disabled = false;
                return;
            }

            const tempEl = document.createElement('div');
            tempEl.innerHTML = result.data.html.trim();
            const newLi = tempEl.firstElementChild;
            if (newLi) li.parentNode.replaceChild(newLi, li);
            return;
        }

        // Düzenle - İptal
        if (e.target.matches('[data-yorum-cancel]')) {
            const li = e.target.closest('.yorum');
            if (!li) return;
            const bodyEl = li.querySelector('[data-yorum-body]');
            const actionsEl = li.querySelector('.yorum__actions');
            bodyEl.innerHTML = bodyEl.dataset.originalHtml || '';
            delete bodyEl.dataset.originalHtml;
            if (actionsEl) actionsEl.hidden = false;
            return;
        }
    });

    // ─── Şikayet modal (Faz 4.10 P1) ─────────────────────────────────────
    const reportModal = document.getElementById('report-modal');
    const reportForm = document.getElementById('report-form');

    if (reportModal && reportForm) {
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

        // Şikayet linki tıklama (event delegation)
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
                // 429 zaten postJson içinde alert gösterdi.
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
    }
})();
