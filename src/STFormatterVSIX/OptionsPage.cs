using System.ComponentModel;
using Microsoft.VisualStudio.Shell;
using STFormatterCore.Configuration;

namespace STFormatterVSIX
{
    /// <summary>
    /// 选项页：工具 → 选项 → ST 格式化。
    /// </summary>
    public class OptionsPage : DialogPage
    {
        // 缩进
        private bool useSpacesInsteadOfTab = true;
        private int indentSize = 4;

        // 空格
        private bool operatorSpacing = true;
        private bool commaSpacing = true;
        private bool parenInnerSpacing = false;

        // 对齐
        private bool alignDeclarations = true;

        // 行
        private int maxLineLength = 120;
        private LineEnding lineEnding = LineEnding.Auto;

        // 空行
        private bool keepEmptyLines = true;
        private int blankLinesAfterVar = 0;
        private int blankLinesBeforeEnd = 0;

        // 类型大小写
        private TypeCase typeCase = TypeCase.Preserve;

        // 行为
        private bool formatOnSave = true;

        [Category("缩进")]
        [DisplayName("使用空格")]
        [Description("使用空格代替 Tab 进行缩进。")]
        public bool UseSpacesInsteadOfTab
        {
            get { return useSpacesInsteadOfTab; }
            set { useSpacesInsteadOfTab = value; }
        }

        [Category("缩进")]
        [DisplayName("缩进空格数")]
        [Description("每个缩进级别使用的空格数。")]
        public int IndentSize
        {
            get { return indentSize; }
            set { indentSize = value; }
        }

        [Category("格式化")]
        [DisplayName("运算符两侧加空格")]
        [Description("在运算符两侧添加空格（例如 a := b + c）。")]
        public bool OperatorSpacing
        {
            get { return operatorSpacing; }
            set { operatorSpacing = value; }
        }

        [Category("格式化")]
        [DisplayName("逗号后加空格")]
        [Description("在逗号后添加空格（例如 Func(a, b, c)）。")]
        public bool CommaSpacing
        {
            get { return commaSpacing; }
            set { commaSpacing = value; }
        }

        [Category("格式化")]
        [DisplayName("对齐变量声明")]
        [Description("对齐 VAR 块中的冒号。")]
        public bool AlignDeclarations
        {
            get { return alignDeclarations; }
            set { alignDeclarations = value; }
        }

        [Category("格式化")]
        [DisplayName("保留空行")]
        [Description("格式化时保留现有的空行。")]
        public bool KeepEmptyLines
        {
            get { return keepEmptyLines; }
            set { keepEmptyLines = value; }
        }

        [Category("格式化")]
        [DisplayName("VAR 块后空行数")]
        [Description("在 VAR...END_VAR 块之后插入的空行数。")]
        public int BlankLinesAfterVar
        {
            get { return blankLinesAfterVar; }
            set { blankLinesAfterVar = value; }
        }

        [Category("格式化")]
        [DisplayName("最大行长度")]
        [Description("超过该长度时换行。")]
        public int MaxLineLength
        {
            get { return maxLineLength; }
            set { maxLineLength = value; }
        }

        [Category("格式化")]
        [DisplayName("换行符")]
        [Description("换行符风格：自动、CRLF 或 LF。")]
        public LineEnding LineEnding
        {
            get { return lineEnding; }
            set { lineEnding = value; }
        }

        [Category("格式化")]
        [DisplayName("括号内侧空格")]
        [Description("在括号内侧添加空格（例如 ( a ) 与 (a)）。")]
        public bool ParenInnerSpacing
        {
            get { return parenInnerSpacing; }
            set { parenInnerSpacing = value; }
        }

        [Category("格式化")]
        [DisplayName("END 前空行数")]
        [Description("在 END_IF、END_FOR 等之前插入的空行数。")]
        public int BlankLinesBeforeEnd
        {
            get { return blankLinesBeforeEnd; }
            set { blankLinesBeforeEnd = value; }
        }

        [Category("格式化")]
        [DisplayName("类型大小写")]
        [Description("类型名称的格式：大写、小写或保持原样。")]
        public TypeCase TypeCase
        {
            get { return typeCase; }
            set { typeCase = value; }
        }

        [Category("行为")]
        [DisplayName("保存时自动格式化")]
        [Description("保存 .TcPOU/.TcDUT/.TcGVL 文件时自动格式化。")]
        public bool FormatOnSave
        {
            get { return formatOnSave; }
            set { formatOnSave = value; }
        }

        /// <summary>
        /// 转换为 FormatterOptions 实例。
        /// </summary>
        public FormatterOptions ToFormatterOptions()
        {
            return new FormatterOptions
            {
                UseSpacesInsteadOfTab = UseSpacesInsteadOfTab,
                IndentSize = IndentSize,
                OperatorSpacing = OperatorSpacing,
                CommaSpacing = CommaSpacing,
                AlignDeclarations = AlignDeclarations,
                KeepEmptyLines = KeepEmptyLines,
                BlankLinesAfterVar = BlankLinesAfterVar,
                BlankLinesBeforeEnd = BlankLinesBeforeEnd,
                MaxLineLength = MaxLineLength,
                LineEnding = LineEnding,
                ParenInnerSpacing = ParenInnerSpacing,
                TypeCase = TypeCase,
            };
        }
    }
}
