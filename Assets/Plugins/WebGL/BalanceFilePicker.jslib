// 브라우저 파일 선택 창으로 xlsx를 골라 "파일 이름|base64" 문자열을 Unity 오브젝트에 보냄
mergeInto(LibraryManager.library, {
  BalancePickFile: function (objectPtr, methodPtr) {
    var target = UTF8ToString(objectPtr);
    var method = UTF8ToString(methodPtr);
    var input = document.createElement('input');
    input.type = 'file';
    input.accept = '.xlsx';
    input.style.display = 'none';
    input.onchange = function () {
      var file = input.files && input.files[0];
      input.remove();
      if (!file) return;
      var reader = new FileReader();
      reader.onload = function () {
        var bytes = new Uint8Array(reader.result);
        var bin = '';
        for (var i = 0; i < bytes.length; i += 0x8000) {
          bin += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
        }
        SendMessage(target, method, file.name + '|' + btoa(bin));
      };
      reader.readAsArrayBuffer(file);
    };
    document.body.appendChild(input);
    input.click();
  }
});
