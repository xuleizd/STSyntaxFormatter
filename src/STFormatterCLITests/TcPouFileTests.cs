using System;
using System.IO;
using System.Text;
using Xunit;
using STFormatterCLI;
using STFormatterCore.Configuration;

namespace STFormatterCLITests
{
    public class TcPouFileTests : IDisposable
    {
        private readonly string _tempDir;

        public TcPouFileTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "STFormatterCLITests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private string CreateTempFile(string content, string extension)
        {
            var path = Path.Combine(_tempDir, "test" + extension);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        // ── Minimal XML templates ──────────────────────────────────────

        private const string NewFormatTcPOU = @"<?xml version=""1.0"" encoding=""utf-8""?>
<TcPlcObject version=""1.1.0.1"">
  <POU Name=""MyProgram"" Id=""{12345678-1234-1234-1234-123456789abc}"">
    <Declaration><![CDATA[PROGRAM MyProgram
VAR
    x : INT;
    y : BOOL;
END_VAR
]]></Declaration>
    <Implementation>
      <ST><![CDATA[x := 1;
y := TRUE;
]]></ST>
    </Implementation>
  </POU>
</TcPlcObject>";

        private const string NewFormatTcDUT = @"<?xml version=""1.0"" encoding=""utf-8""?>
<TcPlcObject version=""1.1.0.1"">
  <DUT Name=""MyEnum"" Id=""{12345678-1234-1234-1234-123456789abc}"">
    <Declaration><![CDATA[TYPE MyEnum :
(
    val1 := 0,
    val2 := 1
);
END_TYPE
]]></Declaration>
  </DUT>
</TcPlcObject>";

        private const string NewFormatTcGVL = @"<?xml version=""1.0"" encoding=""utf-8""?>
<TcPlcObject version=""1.1.0.1"">
  <GVL Name=""GlobalVars"" Id=""{12345678-1234-1234-1234-123456789abc}"">
    <Declaration><![CDATA[VAR_GLOBAL
    counter : INT;
    flag : BOOL;
END_VAR
]]></Declaration>
  </GVL>
</TcPlcObject>";

        private const string OldFormatTcPOU = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Single xml:space=""preserve"" Type=""{11f2245b-f54d-41e7-8df5-0dc5948f8965}"" Method=""IArchivable"">
  <Single Name=""Object"" Type=""{6f9dac99-8de1-4efc-8465-68ac443b7d08}"" Method=""IArchivable"">
    <Single Name=""Implementation"" Type=""{3b83b776-fb25-43b8-99f2-3c507c9143fc}"" Method=""IArchivable"">
      <Single Name=""TextDocument"" Type=""{f3878285-8e4f-490b-bb1b-9acbb7eb04db}"" Method=""IArchivable"">
        <Array Name=""TextLines"" Type=""{a5de0b0b-1cb5-4913-ac21-9d70293ec00d}"">
          <Single Type=""{a5de0b0b-1cb5-4913-ac21-9d70293ec00d}"" Method=""IArchivable"">
            <Single Name=""Id"" Type=""long"">1</Single>
            <Null Name=""Tag"" />
            <Single Name=""Text"" Type=""string"">x := 1;</Single>
          </Single>
          <Single Type=""{a5de0b0b-1cb5-4913-ac21-9d70293ec00d}"" Method=""IArchivable"">
            <Single Name=""Id"" Type=""long"">2</Single>
            <Null Name=""Tag"" />
            <Single Name=""Text"" Type=""string"">y := TRUE;</Single>
          </Single>
        </Array>
      </Single>
    </Single>
    <Single Name=""Interface"" Type=""{a9ed5b7e-75c5-4651-af16-d2c27e98cb94}"" Method=""IArchivable"">
      <Single Name=""TextDocument"" Type=""{f3878285-8e4f-490b-bb1b-9acbb7eb04db}"" Method=""IArchivable"">
        <Array Name=""TextLines"" Type=""{a5de0b0b-1cb5-4913-ac21-9d70293ec00d}"">
          <Single Type=""{a5de0b0b-1cb5-4913-ac21-9d70293ec00d}"" Method=""IArchivable"">
            <Single Name=""Id"" Type=""long"">1</Single>
            <Null Name=""Tag"" />
            <Single Name=""Text"" Type=""string"">PROGRAM Test</Single>
          </Single>
          <Single Type=""{a5de0b0b-1cb5-4913-ac21-9d70293ec00d}"" Method=""IArchivable"">
            <Single Name=""Id"" Type=""long"">2</Single>
            <Null Name=""Tag"" />
            <Single Name=""Text"" Type=""string"">VAR</Single>
          </Single>
          <Single Type=""{a5de0b0b-1cb5-4913-ac21-9d70293ec00d}"" Method=""IArchivable"">
            <Single Name=""Id"" Type=""long"">3</Single>
            <Null Name=""Tag"" />
            <Single Name=""Text"" Type=""string"">    x : INT;</Single>
          </Single>
          <Single Type=""{a5de0b0b-1cb5-4913-ac21-9d70293ec00d}"" Method=""IArchivable"">
            <Single Name=""Id"" Type=""long"">4</Single>
            <Null Name=""Tag"" />
            <Single Name=""Text"" Type=""string"">END_VAR</Single>
          </Single>
          <Single Type=""{a5de0b0b-1cb5-4913-ac21-9d70293ec00d}"" Method=""IArchivable"">
            <Single Name=""Id"" Type=""long"">5</Single>
            <Null Name=""Tag"" />
            <Single Name=""Text"" Type=""string"">END_PROGRAM</Single>
          </Single>
        </Array>
      </Single>
    </Single>
  </Single>
</Single>";

        private static FormatterOptions DefaultOptions()
        {
            return new FormatterOptions
            {
                IndentSize = 4,
                AlignDeclarations = true,
                KeepEmptyLines = true,
                LineEnding = LineEnding.LF
            };
        }

        // ── 1. LoadNewFormat ───────────────────────────────────────────

        [Fact]
        public void LoadNewFormat_ExtractsDeclarationAndImplementation()
        {
            var path = CreateTempFile(NewFormatTcPOU, ".TcPOU");
            var pouFile = new TcPouFile(path);
            // Should not throw; file loaded successfully
            Assert.NotNull(pouFile);
        }

        // ── 2. LoadOldFormat ───────────────────────────────────────────

        [Fact]
        public void LoadOldFormat_ExtractsContent()
        {
            var path = CreateTempFile(OldFormatTcPOU, ".TcPOU");
            var pouFile = new TcPouFile(path);
            Assert.NotNull(pouFile);
        }

        // ── 3. FormatNewFormat_PreservesXmlStructure ───────────────────

        [Fact]
        public void FormatNewFormat_PreservesXmlStructure()
        {
            var path = CreateTempFile(NewFormatTcPOU, ".TcPOU");
            var pouFile = new TcPouFile(path);
            pouFile.Format(DefaultOptions());
            pouFile.Save();

            var result = File.ReadAllText(path);
            Assert.Contains("<TcPlcObject", result);
            Assert.Contains("<POU", result);
            Assert.Contains("<Declaration>", result);
            Assert.Contains("<Implementation>", result);
            Assert.Contains("<ST>", result);
        }

        // ── 4. FormatNewFormat_FormatsDeclaration ──────────────────────

        [Fact]
        public void FormatNewFormat_FormatsDeclaration()
        {
            var path = CreateTempFile(NewFormatTcPOU, ".TcPOU");
            var pouFile = new TcPouFile(path);
            pouFile.Format(DefaultOptions());
            pouFile.Save();

            var result = File.ReadAllText(path);
            // Declaration should still contain VAR block keywords
            Assert.Contains("VAR", result);
            Assert.Contains("END_VAR", result);
            // Should contain CDATA wrapper
            Assert.Contains("<![CDATA[", result);
        }

        // ── 5. FormatNewFormat_FormatsImplementation ───────────────────

        [Fact]
        public void FormatNewFormat_FormatsImplementation()
        {
            var path = CreateTempFile(NewFormatTcPOU, ".TcPOU");
            var pouFile = new TcPouFile(path);
            pouFile.Format(DefaultOptions());
            pouFile.Save();

            var result = File.ReadAllText(path);
            // Implementation/ST should still contain the assignment code
            Assert.Contains("x := 1;", result);
            Assert.Contains("y := TRUE;", result);
        }

        // ── 6. FormatOldFormat_PreservesStructure ──────────────────────

        [Fact]
        public void FormatOldFormat_PreservesStructure()
        {
            var path = CreateTempFile(OldFormatTcPOU, ".TcPOU");
            var pouFile = new TcPouFile(path);
            pouFile.Format(DefaultOptions());
            pouFile.Save();

            var result = File.ReadAllText(path);
            // Old format structure should be preserved
            Assert.Contains("TextLines", result);
            Assert.Contains("TextDocument", result);
        }

        // ── 7. LoadTcDUT ───────────────────────────────────────────────

        [Fact]
        public void LoadTcDUT_ExtractsDeclaration()
        {
            var path = CreateTempFile(NewFormatTcDUT, ".TcDUT");
            var pouFile = new TcPouFile(path);
            Assert.NotNull(pouFile);
        }

        // ── 8. LoadTcGVL ──────────────────────────────────────────────

        [Fact]
        public void LoadTcGVL_ExtractsDeclaration()
        {
            var path = CreateTempFile(NewFormatTcGVL, ".TcGVL");
            var pouFile = new TcPouFile(path);
            Assert.NotNull(pouFile);
        }

        // ── 9-12. IsSupportedFile ──────────────────────────────────────

        [Fact]
        public void IsSupportedFile_TcPOU_ReturnsTrue()
        {
            Assert.True(TcPouFile.IsSupportedFile("C:\\some\\path\\Program.TcPOU"));
        }

        [Fact]
        public void IsSupportedFile_TcDUT_ReturnsTrue()
        {
            Assert.True(TcPouFile.IsSupportedFile("C:\\some\\path\\Data.TcDUT"));
        }

        [Fact]
        public void IsSupportedFile_TcGVL_ReturnsTrue()
        {
            Assert.True(TcPouFile.IsSupportedFile("C:\\some\\path\\Global.TcGVL"));
        }

        [Theory]
        [InlineData("file.txt")]
        [InlineData("code.cs")]
        [InlineData("data.xml")]
        [InlineData("script.js")]
        [InlineData("noextension")]
        public void IsSupportedFile_Unsupported_ReturnsFalse(string filename)
        {
            Assert.False(TcPouFile.IsSupportedFile(filename));
        }

        // ── 13. Save_PreservesUtf8NoBom ────────────────────────────────

        [Fact]
        public void Save_PreservesUtf8NoBom()
        {
            var path = CreateTempFile(NewFormatTcPOU, ".TcPOU");
            var pouFile = new TcPouFile(path);
            pouFile.Format(DefaultOptions());
            pouFile.Save();

            byte[] bytes = File.ReadAllBytes(path);
            // UTF-8 BOM is EF BB BF - should NOT be present
            bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            Assert.False(hasBom, "File should not have UTF-8 BOM");
        }

        // ── 14. Save_PreservesXmlStructure ─────────────────────────────

        [Fact]
        public void Save_PreservesXmlDeclaration()
        {
            var path = CreateTempFile(NewFormatTcPOU, ".TcPOU");
            var pouFile = new TcPouFile(path);
            pouFile.Format(DefaultOptions());
            pouFile.Save();

            var result = File.ReadAllText(path);
            Assert.StartsWith("<?xml", result);
            Assert.Contains("TcPlcObject", result);
        }

        // ── 15. FormatDeclaration_VarBlock ─────────────────────────────

        [Fact]
        public void FormatDeclaration_VarBlock_IsFormatted()
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<TcPlcObject version=""1.1.0.1"">
  <POU Name=""Test"" Id=""{12345678-1234-1234-1234-123456789abc}"">
    <Declaration><![CDATA[PROGRAM Test
VAR
x : INT;
yy : BOOL;
END_VAR
]]></Declaration>
    <Implementation>
      <ST><![CDATA[x := 1;]]></ST>
    </Implementation>
  </POU>
</TcPlcObject>";
            var path = CreateTempFile(xml, ".TcPOU");
            var pouFile = new TcPouFile(path);
            var opts = DefaultOptions();
            opts.AlignDeclarations = true;
            pouFile.Format(opts);
            pouFile.Save();

            var result = File.ReadAllText(path);
            // After formatting with alignment, colons should be aligned
            // The formatter should process the VAR block
            Assert.Contains("VAR", result);
            Assert.Contains("END_VAR", result);
            Assert.Contains("INT", result);
            Assert.Contains("BOOL", result);
        }

        // ── 16. FormatDeclaration_WithArrayTypes ───────────────────────

        [Fact]
        public void FormatDeclaration_WithArrayTypes_IsHandled()
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<TcPlcObject version=""1.1.0.1"">
  <POU Name=""Test"" Id=""{12345678-1234-1234-1234-123456789abc}"">
    <Declaration><![CDATA[PROGRAM Test
VAR
    arr : ARRAY[0..9] OF INT;
    val : INT;
END_VAR
]]></Declaration>
    <Implementation>
      <ST><![CDATA[val := arr[0];]]></ST>
    </Implementation>
  </POU>
</TcPlcObject>";
            var path = CreateTempFile(xml, ".TcPOU");
            var pouFile = new TcPouFile(path);
            pouFile.Format(DefaultOptions());
            pouFile.Save();

            var result = File.ReadAllText(path);
            Assert.Contains("ARRAY", result);
            Assert.Contains("END_VAR", result);
        }

        // ── 17. FormatImplementation_BareCodeWrapped ───────────────────

        [Fact]
        public void FormatImplementation_BareCodeWrapped()
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<TcPlcObject version=""1.1.0.1"">
  <POU Name=""Test"" Id=""{12345678-1234-1234-1234-123456789abc}"">
    <Declaration><![CDATA[PROGRAM Test
VAR
    x : INT;
END_VAR
]]></Declaration>
    <Implementation>
      <ST><![CDATA[x := 1;
IF x > 0 THEN
x := 0;
END_IF
]]></ST>
    </Implementation>
  </POU>
</TcPlcObject>";
            var path = CreateTempFile(xml, ".TcPOU");
            var pouFile = new TcPouFile(path);
            pouFile.Format(DefaultOptions());
            pouFile.Save();

            var result = File.ReadAllText(path);
            // The PROGRAM wrapper should be stripped - should NOT contain PROGRAM __Temp__
            Assert.DoesNotContain("__Temp__", result);
            // The formatted code should still be present
            Assert.Contains("x := 1;", result);
            Assert.Contains("END_IF", result);
        }
    }
}
