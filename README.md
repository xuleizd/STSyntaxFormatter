# STSyntaxFormatter

TwinCAT 3 结构化文本（ST，Structured Text）代码格式化工具：**VSIX 扩展**（Visual Studio 2019 / Beckhoff TcXaeShell）+ **命令行工具**，共享同一个格式化引擎 `STFormatterCore`。

与逐行正则替换的实现不同，本工具走完整的 **Lexer → Parser（递归下降语法树）→ Formatter** 流水线：先真正理解代码结构，再排版，因此不会破坏代码语义——不吞语句、不吞注释、不改字符串字面量，且格式化结果是幂等的（格式化两遍与格式化一遍结果相同）。

## 功能特性

- **关键词统一大写**（TwinCAT 惯例），类型名大小写可选（大写 / 小写 / 保持原样）
- **缩进与换行**：块结构（IF/CASE/FOR/WHILE/REPEAT、VAR 块、STRUCT/UNION、METHOD 等）逐级缩进；跨行 FB 调用续行参数与首参数对齐
- **声明对齐**：VAR 块 / STRUCT 成员的 `:` 对齐（可选）
- **注释被格式化而非搬运**：`//` 后收成一个空格、行内注释离代码固定 4 个空格、独立注释行取它所描述代码的缩进；**绝不删除注释**
- **空行策略**：`--keep-empty-lines`（默认）原样保留源码空行；关闭时仅把连续 2 个以上空行合并成 1 个
- **语法保真**：正确处理 `REPEAT...UNTIL`（UNTIL 后无分号）、`STRUCT RETAIN`（整结构体掉电保持）、`END_TYPE;`、单行/多行枚举、跨行调用注释等容易被打乱的写法
- **文件格式**：支持 `.TcPOU / .TcDUT / .TcGVL` 的新版（`Declaration` / `Implementation/ST` CDATA）与旧版（TextLines 行节点）两种 XML 格式；单独打开的 Method / Property / Action（无 PROGRAM 包裹的裸声明/裸实现体）也能格式化
- **字符串原样保留**：`'...'`、`"..."` 内容绝不被改动

## 格式化效果

格式化前：

```iecst
program Main
var counter:INT; name:STRING(50); end_var
if counter>10 then counter:=0; end_if
```

格式化后（默认选项，声明自动按 `:` 对齐）：

```iecst
PROGRAM Main
VAR
    counter : INT;
    name    : STRING(50);
END_VAR

    IF counter > 10 THEN
        counter := 0;
    END_IF
END_PROGRAM
```

## 安装（VSIX / TcXaeShell）

1. 取 `src/STFormatterVSIX/bin/Release/STFormatterVSIX.vsix`（或从本仓库 Release 下载）。
2. 把 `.vsix`（本质是 zip 包）**解压覆盖**到 TcXaeShell 扩展目录：

   ```
   C:\Program Files (x86)\Beckhoff\TcXaeShell\Common7\IDE\Extensions\STFormatterVSIX\
   ```

   （写入 `Program Files (x86)` 需要管理员权限；解压后如 IDE 正在运行请重启。）
3. 使用方式：
   - 菜单 **工具 → 格式化ST代码**：格式化当前打开的 POU / DUT / GVL（支持 Ctrl+Z 撤销）；
   - **保存时自动格式化**：默认开启，可在 **工具 → 选项 → ST 格式化** 关闭/调整（选项页已中文化）。

在解决方案资源管理器里右键 PLC 项目也可通过命令递归格式化子对象（Action / Method / Property / Transition）。

## 命令行用法（STFormatterCLI）

```
STFormatterCLI.exe -f <文件>            # 格式化单个 .TcPOU/.TcDUT/.TcGVL
STFormatterCLI.exe -p <目录>            # 递归格式化目录下全部 *.TcPOU / *.TcDUT / *.TcGVL

--indentation <n>                       # 缩进列数（默认 4）
--windowslineending / --unixlineending  # 换行符（默认 Auto，跟随原文件风格）
--align-declarations true|false         # VAR/STRUCT 声明冒号对齐（默认 true）
--keep-empty-lines true|false           # 保留源码空行（默认 true；false = 2+ 连续空行合并成 1）
--type-case upper|lower|preserve        # 类型名大小写（默认 preserve）
--skip-validation                       # 跳过写回前的等价性校验（默认开，校验失败的文件保持原样不动）
-v                                      # 详细输出
```

> **注意**：CLI 直接覆盖原文件、不生成 `.bak`，批量处理前请先提交到版本库或自行备份。默认开启的**等价性校验**（token/语法树/注释三重比对）保证格式化绝不会改坏代码——校验不通过的文件会被拒绝写回并计入退出码 1。

## 从源码构建

- **CLI / 测试**（SDK 风格 net48 项目，`dotnet` 即可）：

  ```bash
  dotnet build src/STFormatterCLI/STFormatterCLI.csproj -c Release
  dotnet test src/STFormatterCoreTests/STFormatterCoreTests.csproj -c Debug   # 425 个测试
  dotnet test src/STFormatterCLITests/STFormatterCLITests.csproj -c Debug     # 33 个测试
  ```

- **VSIX**（老式 csproj + VSSDK targets，需 Visual Studio 2019 的 MSBuild）：见 `AGENTS.md` 的 Commands 一节。

## 项目结构

| 项目 | 说明 |
| --- | --- |
| `src/STFormatterCore` | 格式化引擎（Lexer / Parser / Formatter，无 VS 依赖） |
| `src/STFormatterVSIX` | VSIX 扩展（命令、保存时格式化、中文化选项页） |
| `src/STFormatterCLI` | 命令行工具（CommandLineParser，新旧两种 XML 格式读写） |
| `src/STFormatterCoreTests` / `STFormatterCLITests` | xUnit 测试 |
| `testdata/` | 真实 TwinCAT 文件样例 + 回归测试脚手架 |

## 版本历史（摘要）

- **1.9.0** — 依据 Beckhoff《TwinCAT 3 PLC 编程》手册建立语法测试体系（xUnit + FluentAssertions + AutoFixture + Moq，246 个用例）；上线即抓出并修复 19 处排版/词法缺陷（`S=`/`R=` 被拆、下划线数字被拆、一元负号间距、`EXPT (`、`a[2] ()`、`1 .. 5` 标签等）
- **1.8.9** — VSIX 保存路径收敛为单条（消除竞态）；等价性校验接入编辑器写回；失败 InfoBar 提示（带版本号）；CLI 删除无效占位选项、bool 选项可显式关闭；testdata 全样例输出基线
- **1.8.8** — 等价性校验器 + 快照测试体系；修 `IMPLEMENTS` 列表丢逗号、VAR 成员访问修饰符（PUBLIC/PRIVATE）排版断裂
- **1.8.7** — 修 `STRUCT RETAIN` 修饰符被删；STRUCT/UNION 头行行内注释不再丢失
- **1.8.6** — 修 `REPEAT...UNTIL` 吞掉后续语句、凭空补分号的问题；新增全样例不变式扫描
- **1.8.5** — 修块结束关键字（如 `END_STRUCT`）上方注释的缩进归属
- **1.8.4** — 注释由"原样搬运"改为"被格式化"，并修复三处一直存在的丢注释缺陷
- **1.8.3** — 修跨行 FB 调用里注释的三处排版问题
- 更早历史见 git log
