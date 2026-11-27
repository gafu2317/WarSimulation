mergeInto(LibraryManager.library, {
  CountryIsDesktopBrowser: function() {
    var browser = navigator;
    var mobile = (browser.userAgentData && browser.userAgentData.mobile) ||
      /Android|iPhone|iPad|iPod|Mobile/i.test(browser.userAgent) ||
      // iPadOS Safari can identify itself as a Mac.
      (browser.platform === 'MacIntel' && browser.maxTouchPoints > 1);
    return mobile ? 0 : 1;
  }
});
