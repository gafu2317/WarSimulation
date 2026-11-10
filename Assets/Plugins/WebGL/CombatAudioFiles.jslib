mergeInto(LibraryManager.library, {
  $CombatAudioFiles: {
    receiver: null,
    generation: 0,
    nextId: 1,
    buffers: {},
    context: null,
    send: function(method, data) {
      if (CombatAudioFiles.receiver) SendMessage(CombatAudioFiles.receiver, method, JSON.stringify(data));
    },
    read: async function(file, x, y, generation) {
      var state = CombatAudioFiles;
      try {
        if (generation !== state.generation || !state.receiver) return;
        if (file.size > 50 * 1024 * 1024) throw new Error('音声ファイルは50MB以下にしてください');
        if (!state.context) state.context = new (window.AudioContext || window.webkitAudioContext)();
        var audio = await state.context.decodeAudioData(await file.arrayBuffer());
        if (generation !== state.generation || !state.receiver) return;
        if (audio.length * audio.numberOfChannels > 32 * 1024 * 1024)
          throw new Error('音声が長すぎます。短い音声ファイルを選んでください');
        var id = state.nextId++;
        state.buffers[id] = audio;
        try {
          state.send('OnBrowserAudio', { id: id, name: file.name, channels: audio.numberOfChannels,
            frequency: audio.sampleRate, frames: audio.length, x: x, y: y });
        } finally { delete state.buffers[id]; }
      } catch (error) {
        if (generation === state.generation)
          state.send('OnBrowserAudioError', { message: file.name + ': ' + (error.message || '読み込めない音声形式です') });
      }
    },
    over: function(event) {
      if (!CombatAudioFiles.receiver || event.target !== Module.canvas ||
          !event.dataTransfer || !Array.from(event.dataTransfer.types).includes('Files')) return;
      event.preventDefault();
      event.dataTransfer.dropEffect = 'copy';
    },
    drop: function(event) {
      var state = CombatAudioFiles;
      if (!state.receiver || event.target !== Module.canvas || !event.dataTransfer.files.length) return;
      event.preventDefault();
      var rect = Module.canvas.getBoundingClientRect();
      var x = (event.clientX - rect.left) / rect.width;
      var y = 1 - (event.clientY - rect.top) / rect.height;
      var files = Array.from(event.dataTransfer.files);
      var generation = state.generation;
      state.send('OnBrowserAudioStatus', { message: '音声を読み込み中…' });
      files.reduce(function(previous, file, index) {
        return previous.then(function() { return state.read(file, index === 0 ? x : -1, index === 0 ? y : -1, generation); });
      }, Promise.resolve());
    }
  },
  CombatAudioFiles_Enable__deps: ['$CombatAudioFiles'],
  CombatAudioFiles_Enable: function(receiver) {
    CombatAudioFiles.receiver = UTF8ToString(receiver);
    CombatAudioFiles.generation++;
    window.removeEventListener('dragover', CombatAudioFiles.over);
    window.removeEventListener('drop', CombatAudioFiles.drop);
    window.addEventListener('dragover', CombatAudioFiles.over);
    window.addEventListener('drop', CombatAudioFiles.drop);
  },
  CombatAudioFiles_Disable__deps: ['$CombatAudioFiles'],
  CombatAudioFiles_Disable: function() {
    CombatAudioFiles.receiver = null;
    CombatAudioFiles.generation++;
    window.removeEventListener('dragover', CombatAudioFiles.over);
    window.removeEventListener('drop', CombatAudioFiles.drop);
    CombatAudioFiles.buffers = {};
  },
  CombatAudioFiles_Select__deps: ['$CombatAudioFiles'],
  CombatAudioFiles_Select: function() {
    var input = document.createElement('input');
    input.type = 'file';
    input.accept = 'audio/*,.wav,.mp3,.ogg,.m4a,.aac,.flac';
    input.multiple = true;
    var generation = CombatAudioFiles.generation;
    input.onchange = function() {
      var files = Array.from(input.files);
      CombatAudioFiles.send('OnBrowserAudioStatus', { message: '音声を読み込み中…' });
      files.reduce(function(previous, file) {
        return previous.then(function() { return CombatAudioFiles.read(file, -1, -1, generation); });
      }, Promise.resolve());
    };
    input.click();
  },
  CombatAudioFiles_Copy__deps: ['$CombatAudioFiles'],
  CombatAudioFiles_Copy: function(id, destination, count) {
    var audio = CombatAudioFiles.buffers[id];
    if (!audio || count !== audio.length * audio.numberOfChannels) return 0;
    var channels = [];
    for (var c = 0; c < audio.numberOfChannels; c++) channels.push(audio.getChannelData(c));
    var offset = destination >> 2;
    for (var i = 0; i < audio.length; i++)
      for (var channel = 0; channel < channels.length; channel++)
        HEAPF32[offset++] = channels[channel][i];
    return 1;
  }
});
