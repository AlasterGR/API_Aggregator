// Chooses the page's theme and layout. Loaded in <head> without "defer", so both are set before the page is first
// drawn and it never flashes in the wrong colours or layout. It only sets attributes on <html>. The CSS does the rest.
'use strict';

(() => {
  //  The theme choices, where the choice is remembered, and the widths where the layout changes.
  const THEMES = ['system', 'light', 'dark'];
  const THEME_KEY = 'theme';
  const root = document.documentElement;
  const prefersDark = matchMedia('(prefers-color-scheme: dark)');
  const prefersLessMotion = matchMedia('(prefers-reduced-motion: reduce)');
  const wideLayout = matchMedia('(min-width: 641px)');
  const mediumLayout = matchMedia('(min-width: 481px)');

  /** Returns the remembered theme choice, or "system" when there is none or the browser blocks storage. */
  function rememberedTheme() {
    try {
      const value = localStorage.getItem(THEME_KEY);
      return THEMES.includes(value) ? value : 'system';
    } catch {
      return 'system';
    }
  }

  /** Remembers the theme choice, and carries on quietly when the browser blocks storage. */
  function rememberTheme(choice) {
    try {
      localStorage.setItem(THEME_KEY, choice);
    } catch {
      //  The choice still applies to this visit.
    }
  }

  /**
   * Applies a theme choice. "system" follows the device's light or dark setting. When asked, the colours fade over a
   * moment, unless the device asks for less motion.
   */
  function applyTheme(choice, fade = false) {
    //  Fade the colours briefly, when wanted and allowed.
    if (fade && !prefersLessMotion.matches) {
      root.classList.add('theme-changing');
      setTimeout(() => root.classList.remove('theme-changing'), 400);
    }

    //  Record the choice, and the light or dark colours it means right now.
    root.dataset.theme = choice;
    root.dataset.colorScheme = choice === 'system' ? (prefersDark.matches ? 'dark' : 'light') : choice;
  }

  /** Picks the layout for the window's width: wide above 640 px, medium from 481 to 640 px, narrow up to 480 px. */
  function applyLayout() {
    root.dataset.layout = wideLayout.matches ? 'wide' : mediumLayout.matches ? 'medium' : 'narrow';
  }

  /** Connects the theme picker in the header, once the page exists. */
  function connectThemePicker() {
    //  Show the current choice, and apply and remember each new one.
    const picker = document.getElementById('theme-select');
    if (!picker) return;
    picker.value = root.dataset.theme;
    picker.addEventListener('change', () => {
      rememberTheme(picker.value);
      applyTheme(picker.value, true);
    });
  }

  //  Set the theme and layout now, before the page is drawn.
  applyTheme(rememberedTheme());
  applyLayout();

  //  Follow the device when its light or dark setting changes, while the choice is "system".
  prefersDark.addEventListener('change', () => {
    if (root.dataset.theme === 'system') applyTheme('system', true);
  });

  //  Change the layout when the window crosses 640 px or 480 px.
  wideLayout.addEventListener('change', applyLayout);
  mediumLayout.addEventListener('change', applyLayout);

  //  Connect the picker once the page has loaded.
  document.addEventListener('DOMContentLoaded', connectThemePicker);
})();
