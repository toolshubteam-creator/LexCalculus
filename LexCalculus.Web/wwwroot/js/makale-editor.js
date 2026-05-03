// Faz 4.6 P3/3 — Quill editor + tag chip vanilla JS
// MakaleYeni + MakaleDuzenle sayfalarında kullanılır.
(function () {
    'use strict';

    // ─── Quill ─────────────────────────────────────────────────────────
    const editorEl = document.getElementById('quill-editor');
    const bodyInput = document.getElementById('quill-body-input');
    if (!editorEl || !bodyInput || typeof Quill === 'undefined') {
        return;
    }

    const quill = new Quill(editorEl, {
        theme: 'snow',
        placeholder: 'Makalenizi buraya yazın…',
        modules: {
            toolbar: {
                container: [
                    ['bold', 'italic', 'underline'],
                    [{ 'header': 2 }, { 'header': 3 }],
                    [{ 'list': 'ordered' }, { 'list': 'bullet' }],
                    ['blockquote', 'link', 'image'],
                    ['clean']
                ],
                handlers: {
                    'image': customImageHandler
                }
            }
        }
    });

    // Faz 4.8 — clipboard'tan gelen IMG'leri engelle (sadece toolbar upload).
    // Dış URL'li img veya base64 paste yapılırsa kaldırılır.
    const QuillDelta = Quill.import('delta');
    quill.clipboard.addMatcher('IMG', function () {
        return new QuillDelta();
    });

    // Drag-drop kapalı — kullanıcı toolbar üzerinden upload etmeli.
    editorEl.addEventListener('drop', function (e) {
        e.preventDefault();
        e.stopPropagation();
    }, true);
    editorEl.addEventListener('dragover', function (e) {
        e.preventDefault();
        e.stopPropagation();
    }, true);

    function customImageHandler() {
        const input = document.createElement('input');
        input.setAttribute('type', 'file');
        input.setAttribute('accept', 'image/jpeg,image/png,image/webp');

        input.onchange = async function () {
            const file = input.files && input.files[0];
            if (!file) return;

            if (file.size > 5 * 1024 * 1024) {
                alert('Dosya çok büyük (max 5 MB).');
                return;
            }

            const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
            if (!tokenInput) {
                alert('Güvenlik token bulunamadı. Sayfayı yenileyin.');
                return;
            }

            const formData = new FormData();
            formData.append('file', file);

            const range = quill.getSelection(true);
            const placeholderText = '⏳ Yükleniyor…';
            quill.insertText(range.index, placeholderText, { italic: true });
            const placeholderLen = placeholderText.length;

            try {
                const response = await fetch('/api/post-images/upload', {
                    method: 'POST',
                    headers: {
                        'X-CSRF-TOKEN': tokenInput.value
                        // Content-Type'ı browser otomatik (multipart boundary için) belirler
                    },
                    body: formData
                });

                quill.deleteText(range.index, placeholderLen);

                // Faz 5.2 — rate limit (429): kullanıcıya özel mesaj.
                if (response.status === 429) {
                    const retryAfter = response.headers.get('Retry-After') || '60';
                    let rlMsg = 'Çok fazla yükleme. ' + retryAfter + ' saniye sonra tekrar deneyin.';
                    try {
                        const rlData = await response.json();
                        if (rlData && rlData.error) rlMsg = rlData.error;
                    } catch (_) { /* boş body */ }
                    alert(rlMsg);
                    return;
                }

                if (!response.ok) {
                    let errMsg = response.statusText;
                    try {
                        const errData = await response.json();
                        if (errData && errData.error) errMsg = errData.error;
                    } catch (_) { /* JSON parse fail; statusText kullan */ }
                    alert('Yükleme başarısız: ' + errMsg);
                    return;
                }

                const data = await response.json();
                if (!data || !data.url) {
                    alert('Sunucu görsel URL döndürmedi.');
                    return;
                }

                // Defansif: server bypass durumunda da '/' prefix garantisi
                let imageUrl = data.url;
                if (!imageUrl.startsWith('/') && !imageUrl.startsWith('http')) {
                    imageUrl = '/' + imageUrl;
                }

                quill.insertEmbed(range.index, 'image', imageUrl, 'user');
                quill.setSelection(range.index + 1);
            } catch (err) {
                quill.deleteText(range.index, placeholderLen);
                alert('Bağlantı hatası: ' + (err && err.message ? err.message : err));
            }
        };

        input.click();
    }

    // Düzenleme: mevcut Body'yi yükle
    if (bodyInput.value && bodyInput.value.trim().length > 0) {
        quill.root.innerHTML = bodyInput.value;
    }

    const form = bodyInput.closest('form');
    if (form) {
        form.addEventListener('submit', function () {
            // Quill boşsa <p><br></p> üretir — boş bırakma
            const html = quill.root.innerHTML;
            const text = quill.getText().trim();
            bodyInput.value = text.length === 0 ? '' : html;
        });
    }

    // ─── Tag chip ─────────────────────────────────────────────────────
    const tagContainer = document.getElementById('tag-chip-container');
    const tagInput = document.getElementById('tag-chip-input');
    const tagsCsv = document.getElementById('tags-csv-input');
    const MAX_TAGS = 5;
    const MAX_TAG_LEN = 30;
    let tags = [];

    function escapeHtml(s) {
        return s.replace(/[&<>"']/g, function (m) {
            return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[m];
        });
    }

    function renderTags() {
        tagContainer.querySelectorAll('.tag-chip').forEach(function (c) { c.remove(); });
        tags.forEach(function (tag, idx) {
            const chip = document.createElement('span');
            chip.className = 'tag-chip';
            chip.innerHTML = '<span>' + escapeHtml(tag) + '</span>' +
                '<button type="button" data-idx="' + idx + '" aria-label="Etiketi kaldır">×</button>';
            tagContainer.insertBefore(chip, tagInput);
        });
        tagsCsv.value = tags.join(', ');
    }

    function addTag(name) {
        const trimmed = (name || '').trim();
        if (!trimmed) return;
        if (tags.length >= MAX_TAGS) return;
        if (trimmed.length > MAX_TAG_LEN) return;
        const lower = trimmed.toLowerCase();
        if (tags.some(function (t) { return t.toLowerCase() === lower; })) return;
        tags.push(trimmed);
        renderTags();
    }

    if (tagsCsv && tagsCsv.value) {
        tags = tagsCsv.value.split(',')
            .map(function (t) { return t.trim(); })
            .filter(Boolean)
            .slice(0, MAX_TAGS);
        renderTags();
    }

    if (tagInput) {
        tagInput.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' || e.key === ',') {
                e.preventDefault();
                addTag(tagInput.value);
                tagInput.value = '';
            } else if (e.key === 'Backspace' && tagInput.value === '' && tags.length > 0) {
                tags.pop();
                renderTags();
            }
        });
    }

    if (tagContainer) {
        tagContainer.addEventListener('click', function (e) {
            const btn = e.target.closest('.tag-chip button');
            if (!btn) return;
            const idx = parseInt(btn.dataset.idx, 10);
            if (!isNaN(idx)) {
                tags.splice(idx, 1);
                renderTags();
            }
        });
    }
})();
