namespace Hikan.Core
{
    /// <summary>パラメータ整合性チェック違反。発生時は再生成せずエラー停止する。</summary>
    public sealed class HikanValidationException : System.Exception
    {
        public System.Collections.Generic.IReadOnlyList<string> Errors { get; }

        public HikanValidationException(System.Collections.Generic.IReadOnlyList<string> errors)
            : base(string.Join(System.Environment.NewLine, errors))
        {
            Errors = errors;
        }

        public HikanValidationException(string message)
            : this(new string[] { message })
        {
        }
    }
}
