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
        private bool blankLinesAroundStatementBlocks = true;

        // 类型大小写
        private TypeCase typeCase = TypeCase.Preserve;

        // 行为
        private bool formatOnSave = true;
        private bool validateOutput = true;

        [Category("缩进")]
        [DisplayName("使用空格")]
        [Description("使用空格代替 Tab 进行缩进。")]
        [TypeConverter(typeof(ChineseBooleanConverter))]
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
        [TypeConverter(typeof(ChineseBooleanConverter))]
        public bool OperatorSpacing
        {
            get { return operatorSpacing; }
            set { operatorSpacing = value; }
        }

        [Category("格式化")]
        [DisplayName("逗号后加空格")]
        [Description("在逗号后添加空格（例如 Func(a, b, c)）。")]
        [TypeConverter(typeof(ChineseBooleanConverter))]
        public bool CommaSpacing
        {
            get { return commaSpacing; }
            set { commaSpacing = value; }
        }

        [Category("格式化")]
        [DisplayName("对齐变量声明")]
        [Description("对齐 VAR 块中的冒号。")]
        [TypeConverter(typeof(ChineseBooleanConverter))]
        public bool AlignDeclarations
        {
            get { return alignDeclarations; }
            set { alignDeclarations = value; }
        }

        [Category("格式化")]
        [DisplayName("保留空行")]
        [Description("是：完整保留代码里的空行，连续多个也按原数量保留。否：2 个以上连续空行合并成 1 个，单个空行不删。")]
        [TypeConverter(typeof(ChineseBooleanConverter))]
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
        [Description("换行符风格：自动（跟随原文件）、Windows (CRLF) 或 Unix (LF)。")]
        [TypeConverter(typeof(ChineseLineEndingConverter))]
        public LineEnding LineEnding
        {
            get { return lineEnding; }
            set { lineEnding = value; }
        }

        [Category("格式化")]
        [DisplayName("括号内侧空格")]
        [Description("在括号内侧添加空格（例如 ( a ) 与 (a)）。")]
        [TypeConverter(typeof(ChineseBooleanConverter))]
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
        [DisplayName("语句块前后空行")]
        [Description("在最外层语句块（IF / CASE / FOR / WHILE / REPEAT）前后插入分隔空行。关闭后仅保留源码中已有的空行（受“保留空行”控制）。")]
        [TypeConverter(typeof(ChineseBooleanConverter))]
        public bool BlankLinesAroundStatementBlocks
        {
            get { return blankLinesAroundStatementBlocks; }
            set { blankLinesAroundStatementBlocks = value; }
        }

        [Category("格式化")]
        [DisplayName("类型大小写")]
        [Description("类型名称的格式：全部大写、全部小写或保持原样。")]
        [TypeConverter(typeof(ChineseTypeCaseConverter))]
        public TypeCase TypeCase
        {
            get { return typeCase; }
            set { typeCase = value; }
        }

        [Category("行为")]
        [DisplayName("保存时自动格式化")]
        [Description("保存 .TcPOU/.TcDUT/.TcGVL 文件时自动格式化。")]
        [TypeConverter(typeof(ChineseBooleanConverter))]
        public bool FormatOnSave
        {
            get { return formatOnSave; }
            set { formatOnSave = value; }
        }

        [Category("行为")]
        [DisplayName("写回前等价性校验")]
        [Description("是：格式化结果通过 token/语法树/注释三重比对后才写回，校验失败保留原文（强烈建议保持开启）。否：跳过校验直接写回（仅在校验器误报时临时关闭）。")]
        [TypeConverter(typeof(ChineseBooleanConverter))]
        public bool ValidateOutput
        {
            get { return validateOutput; }
            set { validateOutput = value; }
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
                BlankLinesAroundStatementBlocks = BlankLinesAroundStatementBlocks,
                MaxLineLength = MaxLineLength,
                LineEnding = LineEnding,
                ParenInnerSpacing = ParenInnerSpacing,
                TypeCase = TypeCase,
            };
        }
    }
}
