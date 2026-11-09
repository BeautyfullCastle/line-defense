using System.Text;
using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    // 치트 패널의 밸런스 시트 불러오기와 기본값 복원
    public sealed partial class GameController
    {
        void PickBalance()
        {
            if (!BalanceFilePicker.Open(gameObject, nameof(OnBalanceFile), ImportBalance))
                _ui.Toast.Show("이 플랫폼에서는 밸런스 시트를 불러올 수 없습니다.", _ui.CanvasW);
        }

        void ResetBalance()
        {
            var b = BalanceStore.Reset(out string warn);
            ApplyBalance(b);
            _ui.Toast.Show(warn ?? "기본 밸런스 시트로 새 게임을 시작합니다.", _ui.CanvasW);
        }

        /// <summary>웹 빌드의 파일 선택 플러그인이 "이름|base64" 문자열로 부르는 함수입니다.</summary>
        public void OnBalanceFile(string payload)
        {
            if (BalanceFilePicker.TryDecode(payload, out string name, out byte[] bytes)) ImportBalance(name, bytes);
            else _ui.Toast.Show("밸런스 파일을 받지 못했습니다.", _ui.CanvasW);
        }

        void ImportBalance(string name, byte[] bytes)
        {
            if (!BalanceStore.TryImport(bytes, name, out var b, out var diff, out string err))
            {
                _ui.Toast.Show("밸런스 시트 오류: " + err, _ui.CanvasW);
                return;
            }
            ApplyBalance(b);
            string msg = $"밸런스 적용: 기본과 다른 값 {diff.Changed.Count}개 · 새 게임";
            if (diff.Unknown.Count > 0) msg += $" · 모르는 상수 {diff.Unknown.Count}개";
            _ui.Toast.Show(msg, _ui.CanvasW);
            Debug.Log(Describe(name, diff));
        }

        // 판 도중 값을 바꾸면 이미 지은 건물의 저장량이 어긋나므로 새 게임으로 시작함
        void ApplyBalance(Balance b)
        {
            _g = new Game(b);
            _bot = new Bot(_g);
            _ui.Cheat.SetBalanceSource(BalanceStore.ImportedName);
            ResetView();
        }

        static string Describe(string name, BalanceApplyResult diff)
        {
            var sb = new StringBuilder("밸런스 시트 적용: " + name);
            foreach (var ch in diff.Changed) sb.Append($"\n  {ch.Key}: {ch.From} -> {ch.To}");
            if (diff.Unknown.Count > 0) sb.Append("\n  모르는 상수: ").Append(string.Join(", ", diff.Unknown));
            if (diff.Missing.Count > 0) sb.Append("\n  시트에 없어 기본값 유지: ").Append(string.Join(", ", diff.Missing));
            return sb.ToString();
        }
    }
}
