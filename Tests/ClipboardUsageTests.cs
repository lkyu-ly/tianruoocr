using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace TrOCR.Tests
{
    /// <summary>
    /// 架构边界守卫测试：静态扫描 Forms 目录下所有 .cs 文件，
    /// 确保业务代码不直接调用 System.Windows.Forms.Clipboard 的静态 API。
    /// 所有剪贴板操作必须通过 ClipboardHelper 统一获得重试、诊断和异常保护。
    /// </summary>
    [TestFixture]
    public class ClipboardUsageTests
    {
        /// <summary>
        /// 扫描 Forms/**/*.cs 中是否存在对 Clipboard.GetText/ContainsText/SetImage 等
        /// 静态方法的直接调用。如发现违规，测试失败并列出具体文件和行号。
        /// </summary>
        [Test]
        public void Forms_DoNotCallWindowsClipboardDirectly()
        {
            var repositoryRoot = FindRepositoryRoot();
            var formsDirectory = Path.Combine(repositoryRoot, "Forms");

            var violations = Directory
                .EnumerateFiles(formsDirectory, "*.cs", SearchOption.AllDirectories)
                .SelectMany(filePath => FindClipboardCalls(repositoryRoot, filePath))
                .ToArray();

            Assert.That(
                violations,
                Is.Empty,
                "Forms code should use ClipboardHelper for retry and diagnostics:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, violations));
        }

        private static readonly Regex ClipboardApiPattern = new Regex(
            @"\bClipboard\.(GetText|SetText|ContainsText|GetImage|SetImage|GetDataObject|SetDataObject|SetData|GetData|ContainsImage|ContainsData|Clear)\b",
            RegexOptions.Compiled);

        private static IEnumerable<string> FindClipboardCalls(string repositoryRoot, string filePath)
        {
            return File
                .ReadLines(filePath)
                .Select((line, index) => new { line, lineNumber = index + 1 })
                .Where(item => ClipboardApiPattern.IsMatch(item.line))
                .Select(item => $"{GetRelativePath(repositoryRoot, filePath)}:{item.lineNumber}: {item.line.Trim()}");
        }

        private static string FindRepositoryRoot()
        {
            var directory = TestContext.CurrentContext.TestDirectory;
            while (!string.IsNullOrEmpty(directory))
            {
                if (File.Exists(Path.Combine(directory, "TrOCR.sln")))
                {
                    return directory;
                }

                directory = Directory.GetParent(directory)?.FullName;
            }

            throw new DirectoryNotFoundException("无法从测试输出目录定位仓库根目录: " + TestContext.CurrentContext.TestDirectory);
        }

        private static string GetRelativePath(string rootDirectory, string filePath)
        {
            var root = Path.GetFullPath(rootDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(filePath);

            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            return path.Substring(root.Length);
        }
    }
}
