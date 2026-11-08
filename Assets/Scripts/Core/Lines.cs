namespace LineDefense.Core
{
    /// <summary>대사 문자열 표입니다. 각 항목은 [화자, 대사] 쌍입니다.</summary>
    public static class LineTexts
    {
        public static string[] P(string who, string text) => new[] { who, text };

        public static string[][] One(string who, string text) => new[] { P(who, text) };

        public static readonly string[][] Story =
        {
            P("msg", "이영지 장군. 북방 변경의 영지를 하사하노라. 황명이오."),
            P("aide", "마굿간을 지어 노선을 놓고, 농장과 대장간을 요새로 이어 주십시오. 마차가 보급을 나릅니다."),
        };

        public static readonly string[][] Restart =
        {
            P("gen", "성벽은 다시 쌓으면 된다. 이번엔 길부터 제대로 닦는다."),
        };

        public static readonly string[][] FirstWave =
        {
            P("aide", "숲에서 몬스터가 나왔습니다! 요새에 보급이 끊기면 막을 수 없습니다."),
        };




        public static readonly string[] MsgQuotes =
        {
            "폐하께서는 변경의 보고를 과장이라 여기십니다. 다만 이 정도는 허락하셨소.",
            "중앙은 장군의 세력이 커지는 걸 원치 않소. 하나만 고르시오.",
            "대신들 사이에 장군이 사병을 키운다는 말이 돕니다. 신중히 쓰시오.",
            "황명이오. 변경을 지키되, 수도를 넘보지 말라.",
            "이번 허가도 폐하의 은혜임을 잊지 마시오.",
        };
    }
}
