// Faz 6.6 — Tag autocomplete. Mevcut tag chip input'una (makale-editor.js)
// server-side öneri katmanı ekler. Chip oluşturma window.lexAddTag ile yapılır.
// Klavye erişilebilir (ArrowDown/Up + Enter + Escape). XSS: item'lar textContent.
(function () {
    'use strict';

    const tagInput = document.getElementById('tag-chip-input');
    const container = document.getElementById('tag-chip-container');
    if (!tagInput || !container) return;

    const MIN_CHARS = 2;
    const DEBOUNCE_MS = 200;

    const dropdown = document.createElement('div');
    dropdown.className = 'tag-autocomplete';
    dropdown.hidden = true;
    container.appendChild(dropdown);

    let debounceTimer = null;
    let results = [];
    let selectedIndex = -1;

    async function fetchSuggestions(prefix) {
        try {
            const res = await fetch(
                '/api/post-tags/search?q=' + encodeURIComponent(prefix) + '&take=10',
                { headers: { 'Accept': 'application/json' } });
            if (!res.ok) return [];
            return await res.json();
        } catch (e) {
            return [];
        }
    }

    function close() {
        dropdown.hidden = true;
        dropdown.replaceChildren();
        results = [];
        selectedIndex = -1;
    }

    function render(items) {
        results = items;
        selectedIndex = -1;
        dropdown.replaceChildren();

        if (!items.length) {
            dropdown.hidden = true;
            return;
        }

        items.forEach(function (r, i) {
            const item = document.createElement('div');
            item.className = 'tag-autocomplete__item';
            item.dataset.index = i;

            const name = document.createElement('span');
            name.className = 'tag-autocomplete__name';
            name.textContent = r.name;            // textContent → XSS güvenli

            const count = document.createElement('span');
            count.className = 'tag-autocomplete__count';
            count.textContent = r.usageCount;

            item.appendChild(name);
            item.appendChild(count);
            dropdown.appendChild(item);
        });
        dropdown.hidden = false;
    }

    function highlight() {
        const items = dropdown.querySelectorAll('.tag-autocomplete__item');
        items.forEach(function (el, i) {
            el.classList.toggle('is-selected', i === selectedIndex);
        });
    }

    function choose(index) {
        if (index < 0 || index >= results.length) return;
        if (typeof window.lexAddTag === 'function') {
            window.lexAddTag(results[index].name);
        }
        tagInput.value = '';
        close();
        tagInput.focus();
    }

    tagInput.addEventListener('input', function () {
        clearTimeout(debounceTimer);
        const value = tagInput.value.trim();
        if (value.length < MIN_CHARS) { close(); return; }
        debounceTimer = setTimeout(async function () {
            render(await fetchSuggestions(value));
        }, DEBOUNCE_MS);
    });

    tagInput.addEventListener('keydown', function (e) {
        if (dropdown.hidden || results.length === 0) return;
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            selectedIndex = Math.min(selectedIndex + 1, results.length - 1);
            highlight();
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            selectedIndex = Math.max(selectedIndex - 1, -1);
            highlight();
        } else if (e.key === 'Enter' && selectedIndex >= 0) {
            e.preventDefault();   // chip'i öneriden ekle (mevcut Enter=serbest ekleme'yi bastır)
            choose(selectedIndex);
        } else if (e.key === 'Escape') {
            close();
        }
    });

    dropdown.addEventListener('mousedown', function (e) {
        // mousedown: input blur'dan önce yakala
        const item = e.target.closest('.tag-autocomplete__item');
        if (!item) return;
        e.preventDefault();
        choose(parseInt(item.dataset.index, 10));
    });

    document.addEventListener('click', function (e) {
        if (!container.contains(e.target)) close();
    });
})();
