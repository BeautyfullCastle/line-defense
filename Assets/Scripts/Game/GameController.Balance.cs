using System.Text;
using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    // 치트 패널의 밸런스 시트 불러오기와 기본값 복원
    public sealed partial class GameController
    {
        private void PickBalance()
        {
            if (!BalanceFilePicker.Open(gameObject, nameof(OnBalanceFile), ImportBalance))
            {
                _ui.Toast.Show("이 플랫폼에서는 밸런스 시트를 불러올 수 없습니다.", _ui.CanvasWidth);
            }
        }

        private void ResetBalance()
        {
            var balance = BalanceStore.Reset(out string warn);
            ApplyBalance(balance);
            _ui.Toast.Show(warn ?? "기본 밸런스 시트로 새 게임을 시작합니다.", _ui.CanvasWidth);
        }

        /// <summary>웹 빌드의 파일 선택 플러그인이 "이름|base64" 문자열로 부르는 함수입니다.</summary>
        public void OnBalanceFile(string payload)
        {
            if (BalanceFilePicker.TryDecode(payload, out string name, out byte[] bytes))
            {
                ImportBalance(name, bytes);
            }
            else
            {
                _ui.Toast.Show("밸런스 파일을 받지 못했습니다.", _ui.CanvasWidth);
            }
        }

        private void ImportBalance(string name, byte[] bytes)
        {
            if (!BalanceStore.TryImport(bytes, name, out var balance, out var diff, out string errorMessage))
            {
                _ui.Toast.Show("밸런스 시트 오류: " + errorMessage, _ui.CanvasWidth);
                return;
            }
            ApplyBalance(balance);
            string message = $"밸런스 적용: 기본과 다른 값 {diff.Changed.Count}개 · 새 게임";
            if (diff.Unknown.Count > 0)
            {
                message += $" · 모르는 이름 {diff.Unknown.Count}개";
            }

            if (diff.Warnings.Count > 0)
            {
                message += $" · 경고 {diff.Warnings.Count}개";
            }

            _ui.Toast.Show(message, _ui.CanvasWidth);
            if (diff.Warnings.Count > 0)
            {
                Debug.LogWarning(Describe(name, diff));
            }
            else
            {
                Debug.Log(Describe(name, diff));
            }
        }

        // 판 도중 값을 바꾸면 이미 지은 건물의 저장량이 어긋나므로 새 게임으로 시작함
        private void ApplyBalance(Balance balance)
        {
            _game = new Game(balance);
            _bot = new Bot(_game);
            _ui.Cheat.SetBalanceSource(BalanceStore.ImportedName);
            ResetView();
        }

        private static string Describe(string name, BalanceApplyResult diff)
        {
            var builder = new StringBuilder("밸런스 시트 적용: " + name);
            foreach (var change in diff.Changed)
            {
                builder.Append($"\n  {change.Key}: {change.From} -> {change.To}");
            }

            if (diff.Unknown.Count > 0)
            {
                builder.Append("\n  코드에 없는 상수·ID: ").Append(string.Join(", ", diff.Unknown));
            }

            if (diff.Missing.Count > 0)
            {
                builder.Append("\n  시트에 없어 기본값 유지: ").Append(string.Join(", ", diff.Missing));
            }

            foreach (string warning in diff.Warnings)
            {
                builder.Append("\n  경고: ").Append(warning);
            }

            return builder.ToString();
        }
    }
}
