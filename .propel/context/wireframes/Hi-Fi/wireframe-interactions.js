/**
 * wireframe-interactions.js
 * Adds prototype-level interactivity to all Hi-Fi wireframe screens.
 * No backend required — all state changes are purely visual/local.
 */
(function () {
  'use strict';

  /* ── Toast notification system ────────────────────────────── */

  let _toastStack = null;

  function getToastStack() {
    if (!_toastStack) {
      _toastStack = document.createElement('div');
      _toastStack.id = 'wf-toast-stack';
      _toastStack.setAttribute('role', 'status');
      _toastStack.setAttribute('aria-live', 'polite');
      _toastStack.setAttribute('aria-atomic', 'false');
      document.body.appendChild(_toastStack);
    }
    return _toastStack;
  }

  function showToast(msg, type) {
    type = type || 'success';
    const el = document.createElement('div');
    el.className = 'wf-toast wf-toast-' + type;
    el.textContent = msg;
    el.setAttribute('role', 'status');
    getToastStack().appendChild(el);

    requestAnimationFrame(function () {
      requestAnimationFrame(function () {
        el.classList.add('wf-toast-in');
      });
    });

    function dismiss() {
      el.classList.remove('wf-toast-in');
      el.addEventListener('transitionend', function () { el.remove(); }, { once: true });
    }

    el.addEventListener('click', dismiss);
    if (type !== 'error') setTimeout(dismiss, 4500);
  }

  /* ── Smart toast label map ─────────────────────────────────── */

  function labelToMsg(label) {
    var l = (label || '').trim().toLowerCase();
    if (/sign in|log in/.test(l))       return 'Authenticating… Redirecting to your dashboard.';
    if (/create account/.test(l))       return 'Account created successfully. Welcome aboard!';
    if (/update password/.test(l))      return 'Password updated. Redirecting to login…';
    if (/submit intake|submit/.test(l)) return 'Intake submitted and saved.';
    if (/confirm booking/.test(l))      return 'Booking confirmed! Confirmation email sent.';
    if (/add to queue/.test(l))         return 'Patient added to queue — position 4.';
    if (/claim slot/.test(l))           return 'Slot claimed. Appointment confirmed.';
    if (/save version/.test(l))         return 'Template version saved with version history.';
    if (/save/.test(l))                 return 'Changes saved successfully.';
    if (/generate/.test(l))             return 'Report generation started. Ready shortly.';
    if (/invite/.test(l))               return 'Invitation sent to the new user.';
    if (/run verif/.test(l))            return 'Insurance verification complete. See results below.';
    if (/search slots?/.test(l))        return 'Searching available slots…';
    if (/continue to intake/.test(l))   return 'Moving to intake form.';
    if (/export/.test(l))               return 'Export ready for download.';
    if (/download/.test(l))             return 'PDF downloading.';
    if (/export to calendar/.test(l))   return 'Calendar event added.';
    if (/show qr/.test(l))              return 'QR code ready — screenshot to save.';
    if (/print/.test(l))                return 'Opening print dialog.';
    if (/reset/.test(l))                return 'Form cleared.';
    if (/filter/.test(l))               return 'Filter applied.';
    if (/browse files?/.test(l))        return 'File browser opened.';
    if (/resend invite/.test(l))        return 'Invitation resent.';
    if (/rollback/.test(l))             return 'Rolled back to previous version.';
    return 'Done.';
  }

  /* ── Spinner helper ────────────────────────────────────────── */

  function addSpinner(btn) {
    var s = document.createElement('span');
    s.className = 'wf-spinner';
    s.setAttribute('aria-hidden', 'true');
    btn.insertAdjacentElement('afterbegin', s);
    return s;
  }

  /* ── Primary and secondary buttons ────────────────────────── */

  function wireButtons() {
    document.querySelectorAll('.button.primary, .button.secondary').forEach(function (btn) {
      if (btn.tagName === 'A' || btn.dataset.wfWired) return;
      btn.dataset.wfWired = '1';
      btn.style.cursor = 'pointer';

      btn.addEventListener('click', function (e) {
        e.preventDefault();
        if (btn.dataset.wfBusy) return;
        btn.dataset.wfBusy = '1';

        var originalOpacity = btn.style.opacity;
        var spinner = addSpinner(btn);
        btn.style.opacity = '0.76';

        var msg = labelToMsg(btn.textContent);

        setTimeout(function () {
          delete btn.dataset.wfBusy;
          btn.style.opacity = originalOpacity;
          spinner.remove();
          if (msg) showToast(msg, 'success');
        }, 1300);
      });
    });
  }

  /* ── Danger buttons with typed confirmation ────────────────── */

  function wireDangerButtons() {
    document.querySelectorAll('.button.danger').forEach(function (btn) {
      if (btn.tagName === 'A' || btn.dataset.wfDangerWired) return;
      btn.dataset.wfDangerWired = '1';
      btn.style.cursor = 'pointer';

      btn.addEventListener('click', function (e) {
        e.preventDefault();
        if (btn.dataset.wfBusy) return;

        var label = btn.textContent.trim().toLowerCase();
        var needsConfirm = label.includes('acknowledge');

        if (needsConfirm) {
          var scope = btn.closest('.card, section, main');
          var input = scope && scope.querySelector('.input, input[type="text"]');
          var val = input
            ? (input.textContent || input.value || '').trim().toUpperCase()
            : '';
          if (val !== 'ACKNOWLEDGE') {
            showToast('Type ACKNOWLEDGE in the confirmation field to proceed.', 'error');
            return;
          }
        }

        btn.dataset.wfBusy = '1';
        var spinner = addSpinner(btn);
        btn.style.opacity = '0.76';

        setTimeout(function () {
          delete btn.dataset.wfBusy;
          btn.style.opacity = '';
          spinner.remove();
          showToast(
            needsConfirm
              ? 'Critical alert acknowledged and logged.'
              : 'Action completed.',
            'warning'
          );
        }, 900);
      });
    });
  }

  /* ── Ghost / back buttons ──────────────────────────────────── */

  function wireGhostButtons() {
    document.querySelectorAll('.button.ghost').forEach(function (btn) {
      if (btn.tagName === 'A' || btn.dataset.wfGhostWired) return;
      btn.dataset.wfGhostWired = '1';
      btn.style.cursor = 'pointer';

      btn.addEventListener('click', function (e) {
        e.preventDefault();
        var label = btn.textContent.trim().toLowerCase();

        if (label.includes('back')) {
          if (history.length > 1) {
            history.back();
          } else {
            showToast('No previous page in prototype.', 'info');
          }
          return;
        }
        if (label.includes('print')) {
          window.print();
          return;
        }
        if (label.includes('reset') || label.includes('clear')) {
          var scope = btn.closest('section, main, .card');
          if (scope) {
            scope.querySelectorAll('[contenteditable="true"]').forEach(function (f) {
              f.textContent = '';
            });
          }
          showToast('Form cleared.', 'info');
          return;
        }
        if (label.includes('rollback') || label.includes('rollback')) {
          showToast('Rolled back to the previous version.', 'info');
          return;
        }

        var msg = labelToMsg(btn.textContent);
        if (msg) showToast(msg, 'info');
      });
    });
  }

  /* ── Step / wizard navigation ──────────────────────────────── */

  function wireSteps() {
    document.querySelectorAll('.steps').forEach(function (set) {
      var steps = Array.from(set.querySelectorAll('.step'));

      steps.forEach(function (step, i) {
        if (step.dataset.wfStepWired) return;
        step.dataset.wfStepWired = '1';
        step.style.cursor = 'pointer';
        step.setAttribute('role', 'button');
        step.setAttribute('tabindex', '0');

        function activate() {
          steps.forEach(function (s, j) {
            s.classList.remove('active', 'done');
            if (j < i) s.classList.add('done');
            if (j === i) s.classList.add('active');
          });
        }

        step.addEventListener('click', activate);
        step.addEventListener('keydown', function (e) {
          if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); activate(); }
        });
      });
    });
  }

  /* ── File drop zones ──────────────────────────────────────── */

  function wireDropzones() {
    document.querySelectorAll('.dropzone').forEach(function (zone) {
      if (zone.dataset.wfWired) return;
      zone.dataset.wfWired = '1';
      zone.setAttribute('role', 'button');
      zone.setAttribute('tabindex', '0');
      zone.setAttribute('aria-label', 'Drop files here or click to browse');
      zone.style.cursor = 'pointer';
      zone.style.position = 'relative';

      var input = document.createElement('input');
      input.type = 'file';
      input.multiple = true;
      input.accept = '.pdf,.jpg,.jpeg,.png,.tiff';
      input.style.cssText = 'position:absolute;width:0;height:0;opacity:0;pointer-events:none;';
      zone.appendChild(input);

      input.addEventListener('change', function () {
        processFiles(Array.from(input.files || []), zone);
        input.value = '';
      });

      zone.addEventListener('click', function () { input.click(); });
      zone.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); input.click(); }
      });

      zone.addEventListener('dragover', function (e) {
        e.preventDefault();
        zone.classList.add('wf-dragover');
      });
      zone.addEventListener('dragleave', function (e) {
        if (!zone.contains(e.relatedTarget)) zone.classList.remove('wf-dragover');
      });
      zone.addEventListener('drop', function (e) {
        e.preventDefault();
        zone.classList.remove('wf-dragover');
        var files = Array.from((e.dataTransfer && e.dataTransfer.files) || []);
        processFiles(files.length ? files : [{ name: 'dropped-document.pdf', size: 1048576 }], zone);
      });
    });
  }

  function processFiles(files, zone) {
    if (!files.length) {
      files = [{ name: 'sample-document.pdf', size: 2048000 }];
    }
    files.forEach(function (file) {
      var sizeMB = (file.size || 0) / (1024 * 1024);
      if (sizeMB > 10) {
        showToast((file.name || 'File') + ' exceeds the 10 MB limit.', 'error');
        return;
      }
      var ext = ((file.name || '').split('.').pop() || '').toLowerCase();
      if (ext && ['pdf', 'jpg', 'jpeg', 'png', 'tiff'].indexOf(ext) === -1) {
        showToast((file.name || 'File') + ': unsupported format. Use PDF, JPG, PNG, or TIFF.', 'error');
        return;
      }
      renderUploadRow(file, zone);
    });
  }

  function renderUploadRow(file, zone) {
    var row = document.createElement('div');
    row.className = 'wf-upload-row';
    row.innerHTML =
      '<div class="wf-upload-name">' + (file.name || 'document.pdf') + '</div>' +
      '<div class="wf-upload-bar"><div class="wf-upload-fill"></div></div>' +
      '<div class="wf-upload-status">Uploading\u2026</div>';
    zone.insertAdjacentElement('afterend', row);
    animateUpload(row, file.name || 'document.pdf');
  }

  function animateUpload(row, name) {
    var fill = row.querySelector('.wf-upload-fill');
    var status = row.querySelector('.wf-upload-status');
    var pct = 0;
    var stages = [
      [40,  'Uploading\u2026'],
      [72,  'Scanning for malware\u2026'],
      [90,  'Queuing for OCR\u2026'],
      [100, '\u2713 Processed'],
    ];
    var stageIdx = 0;

    var tick = setInterval(function () {
      pct = Math.min(pct + Math.random() * 14 + 5, 100);
      fill.style.width = pct + '%';

      while (stageIdx < stages.length && pct >= stages[stageIdx][0]) {
        status.textContent = stages[stageIdx][1];
        stageIdx++;
      }

      if (pct >= 100) {
        clearInterval(tick);
        fill.style.background = 'var(--success)';
        showToast(name + ' uploaded and processed.', 'success');
      }
    }, 200);
  }

  /* ── Coding suggestion cards (SCR-017) ─────────────────────── */

  function wireSuggestionCards() {
    document.querySelectorAll('.mini-card').forEach(function (card) {
      var acceptBtn = card.querySelector('.button.primary');
      var rejectBtn = card.querySelector('.button.danger');
      var modifyBtn = card.querySelector('.button.ghost');
      if (!acceptBtn && !rejectBtn) return;

      if (acceptBtn && !acceptBtn.dataset.wfCardWired) {
        acceptBtn.dataset.wfCardWired = '1';
        acceptBtn.addEventListener('click', function (e) {
          e.stopPropagation();
          card.style.border = '2px solid var(--success)';
          card.style.background = 'var(--success-soft)';
          [acceptBtn, rejectBtn, modifyBtn].forEach(function (b) {
            if (b) b.style.opacity = '0.35';
          });
          showToast('Code accepted and added to the finalization list.', 'success');
        });
      }

      if (rejectBtn && !rejectBtn.dataset.wfCardWired) {
        rejectBtn.dataset.wfCardWired = '1';
        rejectBtn.addEventListener('click', function (e) {
          e.stopPropagation();
          card.style.border = '2px solid var(--neutral-300)';
          card.style.background = 'var(--neutral-100)';
          card.style.opacity = '0.5';
          card.style.textDecoration = 'line-through';
          showToast('Code rejected.', 'warning');
        });
      }

      if (modifyBtn && !modifyBtn.dataset.wfCardWired) {
        modifyBtn.dataset.wfCardWired = '1';
        modifyBtn.addEventListener('click', function (e) {
          e.stopPropagation();
          var codeEl = card.querySelector('.code, code, strong');
          if (codeEl) {
            codeEl.contentEditable = 'true';
            codeEl.focus();
            card.style.border = '2px solid var(--primary-500)';
            showToast('Edit the code, then press Enter to confirm.', 'info');
            codeEl.addEventListener('keydown', function (ke) {
              if (ke.key === 'Enter') {
                ke.preventDefault();
                codeEl.contentEditable = 'false';
                card.style.border = '2px solid var(--secondary-500)';
                card.style.background = 'rgba(38,166,154,0.06)';
                showToast('Code modified and saved.', 'success');
              }
            }, { once: true });
          }
        });
      }
    });
  }

  /* ── Expandable table rows ─────────────────────────────────── */

  function wireTableRows() {
    document.querySelectorAll('.table-row').forEach(function (row) {
      if (row.dataset.wfWired) return;
      row.dataset.wfWired = '1';
      row.style.cursor = 'pointer';
      row.setAttribute('tabindex', '0');
      row.setAttribute('aria-expanded', 'false');

      function toggle() {
        var next = row.nextElementSibling;
        if (next && next.classList.contains('wf-row-detail')) {
          next.remove();
          row.setAttribute('aria-expanded', 'false');
          return;
        }
        var cells = Array.from(row.children)
          .map(function (c) { return c.textContent.trim().split('\n')[0]; })
          .filter(Boolean);
        var detail = document.createElement('div');
        detail.className = 'wf-row-detail';
        detail.innerHTML = cells
          .map(function (c) { return '<span class="wf-detail-chip">' + c + '</span>'; })
          .join('');
        row.insertAdjacentElement('afterend', detail);
        row.setAttribute('aria-expanded', 'true');
      }

      row.addEventListener('click', toggle);
      row.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggle(); }
      });
    });
  }

  /* ── Editable fields (.input, .textarea, .select) ─────────── */

  function wireEditableFields() {
    document.querySelectorAll('.input, .textarea, .select, .fake-search').forEach(function (el) {
      if (el.dataset.wfWired) return;
      el.dataset.wfWired = '1';
      el.setAttribute('contenteditable', 'true');
      el.setAttribute('spellcheck', 'false');
      el.style.cursor = 'text';
      el.style.outline = 'none';

      el.addEventListener('focus', function () {
        el.style.borderColor = 'var(--primary-500)';
        el.style.boxShadow = '0 0 0 3px rgba(25,118,210,0.18)';
      });
      el.addEventListener('blur', function () {
        el.style.borderColor = '';
        el.style.boxShadow = '';
      });
      // Prevent enter from creating new blocks in single-line inputs
      if (el.classList.contains('input') || el.classList.contains('select') || el.classList.contains('fake-search')) {
        el.addEventListener('keydown', function (e) {
          if (e.key === 'Enter') e.preventDefault();
        });
      }
    });
  }

  /* ── Navigation bar items ──────────────────────────────────── */

  function wireNavItems() {
    document.querySelectorAll('.nav-item').forEach(function (item) {
      if (item.dataset.wfWired) return;
      item.dataset.wfWired = '1';
      item.style.cursor = 'pointer';
      item.setAttribute('role', 'button');
      item.setAttribute('tabindex', '0');

      item.addEventListener('click', function () {
        item.closest('.nav-group')
          .querySelectorAll('.nav-item')
          .forEach(function (n) { n.classList.remove('active'); });
        item.classList.add('active');
        showToast('Navigating to ' + item.textContent.trim() + '\u2026', 'info');
      });
      item.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); item.click(); }
      });
    });
  }

  /* ── Slot cards (SCR-004) ──────────────────────────────────── */

  function wireSlotCards() {
    var table = document.querySelector('.table');
    if (!table) return;
    document.querySelectorAll('.table-row').forEach(function (row) {
      if (row.dataset.wfSlotWired) return;
      row.dataset.wfSlotWired = '1';
      var statusCell = row.querySelector('.badge');
      row.addEventListener('click', function () {
        document.querySelectorAll('.table-row .badge').forEach(function (b) {
          b.textContent = 'Available';
          b.className = 'badge';
        });
        if (statusCell) {
          statusCell.textContent = 'Selected';
          statusCell.className = 'badge success';
        }
        // Update sticky footer
        var footer = document.querySelector('.footer-bar strong');
        if (footer) {
          var date = row.children[0] && row.children[0].textContent.trim();
          var time = row.children[1] && row.children[1].textContent.trim();
          var dr = row.children[2] && row.children[2].textContent.trim();
          if (date && time) footer.textContent = 'Selected slot: ' + date + ', ' + time + (dr ? ' with ' + dr : '');
        }
      });
    });
  }

  /* ── Init ─────────────────────────────────────────────────── */

  function init() {
    wireButtons();
    wireDangerButtons();
    wireGhostButtons();
    wireSteps();
    wireDropzones();
    wireSuggestionCards();
    wireTableRows();
    wireEditableFields();
    wireNavItems();
    wireSlotCards();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
