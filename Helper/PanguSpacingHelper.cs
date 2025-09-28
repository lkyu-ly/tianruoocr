using System.Text;

namespace TrOCR.Helper
{
    public static class PanguSpacingHelper
    {
        // 定义字符类型枚举
        private enum CharType
        {
            CJK,          // 中日韩字符
            Latin,        // 西文（字母、数字）
            Symbol,       // 西文符号
            Other
        }

        /// <summary>
        /// 获取字符的详细类型
        /// </summary>
        private static CharType GetCharType(char c)
        {
            // CJK 范围
            if ((c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3040 && c <= 0x309F) || (c >= 0x30A0 && c <= 0x30FF))
                return CharType.CJK;

            // 拉丁字母和数字
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                return CharType.Latin;

            // 常见的西文符号            
            if ("`~!@#$%^&*()_+-=[]{}\\|;:'\",./<>?".IndexOf(c) != -1)
                return CharType.Symbol;

            return CharType.Other;
        }

        /// <summary>
        /// 应用“盘古之白”排版规则（完整版）
        /// </summary>
        /// <param name="text">需要处理的原始文本</param>
        /// <returns>经过排版处理后的文本</returns>
        public static string ApplyPanguSpacing(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 2)
                return text;

            var sb = new StringBuilder(text.Length + 50);
            sb.Append(text[0]);

            for (int i = 1; i < text.Length; i++)
            {
                char lastChar = text[i - 1];
                char currentChar = text[i];

                var lastType = GetCharType(lastChar);
                var currentType = GetCharType(currentChar);

                bool needSpace = false;

                // 规则1: CJK <=> 西文(字母/数字)
                if ((lastType == CharType.CJK && currentType == CharType.Latin) ||
                    (lastType == CharType.Latin && currentType == CharType.CJK))
                {
                    needSpace = true;
                }
                // 规则2: CJK <=> 西文符号 (智能判断)
                else if ((lastType == CharType.CJK && currentType == CharType.Symbol) ||
                         (lastType == CharType.Symbol && currentType == CharType.CJK))
                {
                    // 默认需要加空格，然后用一系列“例外”来否定它
                    needSpace = true;

                    // --- 智能判断逻辑开始 ---


                    // 例外1: 智能处理开括号 "CJK + ("
                    if (lastType == CharType.CJK && "([{<".IndexOf(currentChar) != -1)
                    {
                        if (i + 1 < text.Length)
                        {
                            // 仅当括号内是西文时，才加空格
                            needSpace = GetCharType(text[i + 1]) == CharType.Latin;
                        }
                    }
                    // 例外2: 处理所有闭合性标点紧跟 CJK " ) + CJK "
                    else if (")]}>”'\"".IndexOf(lastChar) != -1 && currentType == CharType.CJK)
                    {
                        needSpace = false;
                    }
                    // 例外3 (新增的核心): 处理所有 CJK 紧跟闭合性标点 " CJK + ) "
                    else if (lastType == CharType.CJK && ")]}>”'\"".IndexOf(currentChar) != -1)
                    {
                        needSpace = false;
                    }
                    // 例外4 (新增的核心): 处理所有开引号紧跟 CJK " “ + CJK "
                    else if (lastType == CharType.Symbol && "“'\"".IndexOf(lastChar) != -1 && currentType == CharType.CJK)
                    {
                        needSpace = false;
                    }
                }
                if (needSpace)
                {
                    sb.Append(' ');
                }

                sb.Append(currentChar);
            }
            /**或者
             for (int i = 1; i < text.Length; i++)
            {
                char lastChar = text[i - 1];
                char currentChar = text[i];
                var lastType = GetCharType(lastChar);
                var currentType = GetCharType(currentChar);
                bool needSpace = false;

                if ((lastType == CharType.CJK && currentType == CharType.Latin) ||
                    (lastType == CharType.Latin && currentType == CharType.CJK))
                {
                    needSpace = true;
                }
                else if ((lastType == CharType.CJK && currentType == CharType.Symbol) ||
                         (lastType == CharType.Symbol && currentType == CharType.CJK))
                {
                    // --- 最终的、最完整的智能判断逻辑 ---

                    // 例外1: 智能处理开括号 "CJK + ("
                    if (lastType == CharType.CJK && "([{<".IndexOf(currentChar) != -1)
                    {
                        if (i + 1 < text.Length)
                        {
                            needSpace = GetCharType(text[i + 1]) == CharType.Latin;
                        }
                        else { needSpace = true; }
                    }
                    // 例外2: CJK 紧跟各种闭合性标点 (e.g., 你好")
                    else if (lastType == CharType.CJK && ")]}>”'\"".IndexOf(currentChar) != -1)
                    {
                        needSpace = false;
                    }
                    // 例外3: 开引号/开书名号 紧跟 CJK (e.g., "你好)
                    else if ("“'\"".IndexOf(lastChar) != -1 && currentType == CharType.CJK)
                    {
                        needSpace = false;
                    }
                    // 例外4: 闭合性标点 紧跟 CJK (e.g., )你好)
                    else if (")]}>”'\"".IndexOf(lastChar) != -1 && currentType == CharType.CJK)
                    {
                        needSpace = false;
                    }
                    // 默认规则: 以上都不是，则说明是普通符号，需要加空格
                    else
                    {
                        needSpace = true;
                    }
                }

                if (needSpace)
                {
                    sb.Append(' ');
                }
                sb.Append(currentChar);
            }
            */

            return sb.ToString();
        }
    }
}
