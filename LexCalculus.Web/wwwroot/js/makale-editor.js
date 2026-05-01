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
            toolbar: [
                ['bold', 'italic', 'underline'],
                [{ 'header': 2 }, { 'header': 3 }],
                [{ 'list': 'ordered' }, { 'list': 'bullet' }],
                ['blockquote', 'link'],
                ['clean']
            ]
        }
    });

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
