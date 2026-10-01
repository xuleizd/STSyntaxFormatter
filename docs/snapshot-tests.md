# 快照格式化测试约定（Snapshot Tests）

代码：`src/STFormatterCoreTests/SnapshotFormattingTests.cs`；
数据：`src/STFormatterCoreTests/Snapshots/<分类>/<名称>.test`。

对标 CSharpier 的 FormattingTests：**每个语法构造一个 `.test` 文件**（约 70 个起步，
随新语法支持继续加），输入故意写成不规整形态，锻炼排版路径。

## 文件约定

| 文件 | 角色 |
|---|---|
| `X.test` | 输入（不规整源码）。**人工编写，永不自动改写**。 |
| `X.expected.test` | 期望输出（格式化契约）。由 STF_SNAPSHOT_UPDATE 生成，**生成后必须人工审查再提交**。 |
| `X.actual.test` | 失败时的实际输出（诊断用）。**git 忽略，不提交**。 |

## 每个用例断言三条不变式

1. `Format(input) == expected` —— 排版被钉死
2. `FormattingValidator.Validate(input, actual)` 通过 —— 不吞 token、不改树、不丢注释
   （见 [equivalence-validator.md](equivalence-validator.md)）
3. `Format(actual) == actual` —— 幂等

默认选项（LF、缩进 4、对齐开、KeepEmptyLines=true）；选项变体的覆盖由
`DutDeclarationTests`/`FormatterTests` 等既有测试承担。

## 工作流

新增用例：
```bash
# 1. 写 Snapshots/<分类>/<名称>.test（只写输入）
# 2. 生成期望文件
STF_SNAPSHOT_UPDATE=1 dotnet test src/STFormatterCoreTests/STFormatterCoreTests.csproj \
  --filter "FullyQualifiedName~SnapshotFormattingTests"
# 3. 人工审查生成的 .expected.test（它就是契约！）
# 4. git add X.test X.expected.test
```

正常跑（无环境变量）即校验三条不变式。

失败调试：
- 失败消息内嵌 unified diff；实际输出已写 `X.actual.test`。
- 想弹 diff 工具：设 `STF_SHOW_DIFF=1`；默认自动探测 WinMerge
  （`C:\Program Files[ (x86)]\WinMerge\WinMerge.exe`），没有 WinMerge 时用
  `STF_DIFF_TOOL` 指定完整命令行，例如 `STF_DIFF_TOOL="code --diff"`。

## 历史战绩

- 上线当天抓到两个存量 bug：
  1. `FUNCTION_BLOCK ... IMPLEMENTS I_A, I_B` 的逗号被删（输出 `I_A I_B`，编译不过）
     ——由「期望文件由当前引擎生成 + 校验器拒收」组合抓到；
  2. `PUBLIC nPublic : INT;` 成员访问修饰符排版断裂（类型被拆到下一行）——由期望输出
     人工审查抓到。
- 快照基线一旦提交，任何排版行为变化都会在 `git diff` 里显形：改规则的人必须同时
  更新受影响的 `.expected.test`，波及面一目了然。
