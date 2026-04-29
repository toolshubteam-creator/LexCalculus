(function () {
  'use strict';

  function init() {
    var select = document.getElementById('Input_MeslekTuru');
    var wrapper = document.querySelector('[data-meslek-diger-wrapper]');
    if (!select || !wrapper) return;

    function toggle() {
      if (select.value === '99') {
        wrapper.classList.remove('is-hidden');
      } else {
        wrapper.classList.add('is-hidden');
      }
    }

    select.addEventListener('change', toggle);
    toggle(); // initial state on page load (ModelState validation için)
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
