// /profil sayfası — Faz 4.1 P3-fix.
// Profil bağlantısı kopyala butonu (Clipboard API + execCommand fallback).
(function () {
    'use strict';

    function flashCopied(btn, originalText) {
        btn.setAttribute('data-copied', 'true');
        btn.textContent = 'Kopyalandı';
        setTimeout(function () {
            btn.removeAttribute('data-copied');
            btn.textContent = originalText;
        }, 2000);
    }

    function legacyCopy(text) {
        var ta = document.createElement('textarea');
        ta.value = text;
        ta.setAttribute('readonly', '');
        ta.setAttribute('aria-hidden', 'true');
        ta.className = 'visually-hidden-textarea';
        document.body.appendChild(ta);
        ta.select();
        var ok = false;
        try { ok = document.execCommand('copy'); } catch (e) { ok = false; }
        document.body.removeChild(ta);
        return ok;
    }

    document.addEventListener('DOMContentLoaded', function () {
        var buttons = document.querySelectorAll('.profil-link__copy');
        buttons.forEach(function (btn) {
            btn.addEventListener('click', function () {
                var url = btn.getAttribute('data-copy');
                if (!url) return;
                var originalText = btn.textContent;

                if (navigator.clipboard && window.isSecureContext) {
                    navigator.clipboard.writeText(url).then(
                        function () { flashCopied(btn, originalText); },
                        function () {
                            if (legacyCopy(url)) flashCopied(btn, originalText);
                        }
                    );
                } else if (legacyCopy(url)) {
                    flashCopied(btn, originalText);
                }
            });
        });

        // Faz 6.2 P2 — granüler e-posta tercihleri: master ("E-posta bildirimleri al")
        // kapalıyken grubu görsel olarak pasifleştir. Input'lar disabled DEĞİL —
        // değerleri POST'ta korunur (disabled checkbox POST etmez).
        var master = document.getElementById('Input_NotificationsEmailEnabled');
        var granular = document.querySelector('[data-email-granular]');
        if (master && granular) {
            var syncGranular = function () {
                granular.classList.toggle('is-dimmed', !master.checked);
            };
            master.addEventListener('change', syncGranular);
            syncGranular();
        }
    });
})();
