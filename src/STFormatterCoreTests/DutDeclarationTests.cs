using STFormatterCore.Configuration;
using STFormatterCore.Formatter;
using STFormatterCore.Lexer;
using STFormatterCore.Parser;
using Xunit;

namespace STFormatterCoreTests
{
    /// <summary>
    /// DUT declarations exactly as TwinCAT stores them in .TcDUT files: a TYPE
    /// header, a body (STRUCT / UNION / enum / alias) and END_TYPE. TwinCAT's own
    /// indentation of these is inconsistent — the body opener may sit flush with
    /// TYPE or one level in, members may or may not be indented, an alias may end
    /// with its ';' on a line of its own — so every case here pins the layout the
    /// formatter normalizes to and requires it to be a fixed point in both
    /// KeepEmptyLines modes. Fixtures: testdata/DUTs_DB.
    /// </summary>
    public class DutDeclarationTests
    {
        private static string Format(string source, bool keepEmptyLines)
        {
            var options = new FormatterOptions
            {
                KeepEmptyLines = keepEmptyLines,
                LineEnding = LineEnding.LF
            };
            var tokens = new STLexer(source).Tokenize();
            var cst = new STParser(tokens).Parse();
            return new STFormatter(options).Format(cst, source);
        }

        /// <summary>
        /// Asserts the exact formatted layout and that reformatting the result
        /// changes nothing — in both KeepEmptyLines modes.
        /// </summary>
        private static void AssertLayout(string source, string expected)
        {
            foreach (var keep in new[] { true, false })
            {
                var once = Format(source, keep);
                Assert.True(once == expected,
                    $"KeepEmptyLines={keep}: unexpected layout\n--- expected ---\n{expected}\n--- actual ---\n{once}");
                Assert.True(Format(once, keep) == once,
                    $"KeepEmptyLines={keep}: formatting a DUT twice must be a fixed point\n{once}");
            }
        }

        [Fact]
        public void TypeAlias_SemicolonOnItsOwnLine_JoinsTheHeader()
        {
            // TwinCAT writes "TYPE sss : DATE_AND_TIME" with the terminating ';' on
            // the next line; it belongs to the header, not to a statement of its own.
            AssertLayout(
                "TYPE sss : DATE_AND_TIME\n;\nEND_TYPE\n",
                "TYPE sss : DATE_AND_TIME;\nEND_TYPE\n");
        }

        [Fact]
        public void TypeAlias_ArraySpec_KeepsBracketsAttached()
        {
            AssertLayout(
                "TYPE TArr : ARRAY[1..10] OF INT;\nEND_TYPE\n",
                "TYPE TArr : ARRAY[1..10] OF INT;\nEND_TYPE\n");
        }

        [Fact]
        public void StructBody_SitsFlushWithHeader_MembersIndentOneLevel()
        {
            AssertLayout(
                "TYPE Line_rfid :\n" +
                "    STRUCT\n" +
                "    ID    : LINT;\n" +
                "    SXBID : STRING(50);\n" +
                "    ZCID  : STRING(80);\n" +
                "    END_STRUCT\n" +
                "END_TYPE\n",
                "TYPE Line_rfid :\n" +
                "STRUCT\n" +
                "    ID    : LINT;\n" +
                "    SXBID : STRING(50);\n" +
                "    ZCID  : STRING(80);\n" +
                "END_STRUCT\n" +
                "END_TYPE\n");
        }

        [Fact]
        public void StructBody_AlreadyFlush_StaysPut_AndRealignmentKeepsColumns()
        {
            AssertLayout(
                "{attribute 'pack_mode' := '1'}\n" +
                "TYPE InsertEntry :\n" +
                "    STRUCT\n" +
                "    JS         : WSTRING(50);\n" +
                "    FTPBROWSE  : BOOL;\n" +
                "    END_STRUCT\n" +
                "END_TYPE\n",
                "{attribute 'pack_mode' := '1'}\n" +
                "TYPE InsertEntry :\n" +
                "STRUCT\n" +
                "    JS        : WSTRING(50);\n" +
                "    FTPBROWSE : BOOL;\n" +
                "END_STRUCT\n" +
                "END_TYPE\n");
        }

        [Fact]
        public void UnionBody_SameLayoutAsStruct()
        {
            AssertLayout(
                "TYPE sdd :\n" +
                "    UNION\n" +
                "    i : INT;\n" +
                "    END_UNION\n" +
                "END_TYPE\n",
                "TYPE sdd :\n" +
                "UNION\n" +
                "    i : INT;\n" +
                "END_UNION\n" +
                "END_TYPE\n");
        }

        [Fact]
        public void StructRetain_ModifierStaysOnTheStructHeaderLine()
        {
            // TwinCAT 3 can declare a whole struct as retain data — "STRUCT RETAIN"
            // (the Retain Handler then manages every instance of the type, see the
            // Beckhoff 'TcRetain' docs). The modifier is part of the header, not a
            // member: it stays on the STRUCT line and must survive formatting.
            AssertLayout(
                "TYPE ST_Counter :\n" +
                "    STRUCT RETAIN\n" +
                "    nValue : INT;\n" +
                "    END_STRUCT\n" +
                "END_TYPE\n",
                "TYPE ST_Counter :\n" +
                "STRUCT RETAIN\n" +
                "    nValue : INT;\n" +
                "END_STRUCT\n" +
                "END_TYPE\n");
        }

        [Fact]
        public void StructRetain_OnItsOwnLine_JoinsTheHeader()
        {
            AssertLayout(
                "TYPE ST_C :\n" +
                "STRUCT\n" +
                "RETAIN\n" +
                "x : BOOL;\n" +
                "END_STRUCT\n" +
                "END_TYPE\n",
                "TYPE ST_C :\n" +
                "STRUCT RETAIN\n" +
                "    x : BOOL;\n" +
                "END_STRUCT\n" +
                "END_TYPE\n");
        }

        [Fact]
        public void StructRetain_LowercaseKeywords_UppercasedNotDeleted()
        {
            AssertLayout(
                "TYPE ST_C : struct retain\n" +
                "x : BOOL;\n" +
                "end_struct\n" +
                "END_TYPE\n",
                "TYPE ST_C :\n" +
                "STRUCT RETAIN\n" +
                "    x : BOOL;\n" +
                "END_STRUCT\n" +
                "END_TYPE\n");
        }

        [Fact]
        public void StructPersistent_ModifierStaysOnTheHeaderLine()
        {
            AssertLayout(
                "TYPE ST_P : STRUCT PERSISTENT\nx : BOOL;\nEND_STRUCT\nEND_TYPE\n",
                "TYPE ST_P :\n" +
                "STRUCT PERSISTENT\n" +
                "    x : BOOL;\n" +
                "END_STRUCT\n" +
                "END_TYPE\n");
        }

        [Fact]
        public void StructHeader_TrailingComment_IsKeptOnTheHeaderLine()
        {
            AssertLayout(
                "TYPE S : STRUCT // header comment\nx : BOOL;\nEND_STRUCT\nEND_TYPE\n",
                "TYPE S :\n" +
                "STRUCT    // header comment\n" +
                "    x : BOOL;\n" +
                "END_STRUCT\n" +
                "END_TYPE\n");
        }

        [Fact]
        public void UnionRetain_ModifierStaysOnTheHeaderLine()
        {
            AssertLayout(
                "TYPE U_R : UNION RETAIN\ni : INT;\nEND_UNION\nEND_TYPE\n",
                "TYPE U_R :\n" +
                "UNION RETAIN\n" +
                "    i : INT;\n" +
                "END_UNION\n" +
                "END_TYPE\n");
        }

        [Fact]
        public void EnumBody_MultiLine_KeepsLineBreaks_NormalizesIndent()
        {
            // '(' and ');' belong at the TYPE declaration's indent, the values one
            // level deeper — whatever the source did.
            AssertLayout(
                "{attribute 'qualified_only'}\n" +
                "{attribute 'strict'}\n" +
                "TYPE DUT :\n" +
                "    (\n" +
                "    enum_member := 0\n" +
                "    );\n" +
                "END_TYPE\n",
                "{attribute 'qualified_only'}\n" +
                "{attribute 'strict'}\n" +
                "TYPE DUT :\n" +
                "(\n" +
                "    enum_member := 0\n" +
                ");\n" +
                "END_TYPE\n");
        }

        [Fact]
        public void EnumBody_SingleLine_StaysOnOneLine()
        {
            // The break before END_TYPE sits in the ';' trailing trivia. Counting it
            // as part of the enum's own layout made the second pass break ')' onto a
            // line of its own.
            AssertLayout(
                "TYPE E_Color : (Red, Green, Blue);\nEND_TYPE\n",
                "TYPE E_Color :\n(Red, Green, Blue);\nEND_TYPE\n");
        }

        [Fact]
        public void EnumBody_SingleLineWithBaseType_KeepsOperatorSpacing()
        {
            const string expected =
                "TYPE E_Size :\n(Small := 1, Big := 2) : INT;\nEND_TYPE\n";

            AssertLayout("TYPE E_Size : (Small := 1, Big := 2) : INT;\nEND_TYPE\n", expected);
            // A source that lost its spacing is repaired to the same layout.
            AssertLayout("TYPE E_Size : (Small:=1, Big:=2):INT;\nEND_TYPE\n", expected);
        }

        [Fact]
        public void TypeHeader_ExtraSpacesBeforeColon_Collapse()
        {
            AssertLayout(
                "TYPE EntrySearch  :\n" +
                "STRUCT\n" +
                "    ID : LINT;\n" +
                "END_STRUCT\n" +
                "END_TYPE\n",
                "TYPE EntrySearch :\n" +
                "STRUCT\n" +
                "    ID : LINT;\n" +
                "END_STRUCT\n" +
                "END_TYPE\n");
        }

        [Fact]
        public void EndTypeWithSemicolon_KeepsItOnTheSameLine()
        {
            // "END_TYPE;" is accepted by TwinCAT; the ';' must not drift onto a line
            // of its own, and the trailing blank line of the CDATA is not kept.
            AssertLayout(
                "{attribute 'pack_mode' := '8'} \n" +
                "TYPE TableEntry :\n" +
                "STRUCT\n" +
                "    ID : LINT;\n" +
                "    JS : WSTRING(50);\n" +
                "END_STRUCT\n" +
                "END_TYPE;\n" +
                "\n",
                "{attribute 'pack_mode' := '8'}\n" +
                "TYPE TableEntry :\n" +
                "STRUCT\n" +
                "    ID : LINT;\n" +
                "    JS : WSTRING(50);\n" +
                "END_STRUCT\n" +
                "END_TYPE;\n");
        }
    }
}
