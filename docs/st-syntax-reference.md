# TwinCAT 3 ST 语法参考（格式化器测试基线）

来源：Beckhoff《TwinCAT 3 PLC 编程》中文手册（TE1000，版本 3.1.0，2024-11-29）第 16 章「编程」。
本文档是 **语法测试的权威清单**：每个构造一条，标注本项目格式化器支持度，供 `STSyntaxSpecTests` 与快照测试对照。
支持度图例：✅ 已支持且有测试 · 🔶 词法/解析路径覆盖但缺专门测试 · ⚠️ 已知不支持（排版劣化，但等价性校验必须保住）· ❓ 未验证

## 1. 指令（16.1.3.5）

| 构造 | 语法 | 示例 | 支持度 |
|---|---|---|---|
| 赋值 | `x := expr;` | `nVar := 1;` | ✅ |
| 多重赋值（ExST） | `a := b := expr;` | `nA := nB := nC + 9;` | 🔶 |
| 输出赋值 | `out => var;`（右侧可空） | `F(x, y => nVar);` / `y => ;` | 🔶 |
| 置位赋值（ExST） | `var S= operand;` | `bSet S= bOperand;` | 🔶 |
| 复位赋值（ExST） | `var R= operand;` | `bReset R= bOperand;` | 🔶 |
| 引用赋值 | `ref REF= var;` | `refA REF= stA;` | 🔶（RefAssign token） |
| IF / ELSIF / ELSE | `IF c THEN ... ELSIF c THEN ... ELSE ... END_IF` | | ✅ |
| FOR（BY 可选） | `FOR i := a TO b {BY s} DO ... END_FOR` | | ✅ |
| CASE（多标签/范围/ELSE） | `CASE v OF 1,5: ...; 10..20: ...; ELSE ... END_CASE` | | ✅ |
| WHILE | `WHILE c DO ... END_WHILE` | | ✅ |
| REPEAT（UNTIL 后无分号） | `REPEAT ... UNTIL c END_REPEAT` | | ✅（1.8.6） |
| RETURN | `RETURN;` | | ✅ |
| EXIT | `EXIT;` | | ✅ |
| CONTINUE（ExST） | `CONTINUE;` | | ✅ |
| JMP + 标签 | `_label1 : stmt; ... JMP _label1;` | | ⚠️ 标签行未专门解析（verbatim 类） |
| FB 调用 | `fbTMR(IN := %OX5, PT := T#300ms);` | | ✅ |
| 函数调用作操作数 | `n := F_Add(7,5) + 3;` | | ✅ |
| 指针 FB 调用 | `pFB^(nIn := 1, nOut => nLoc);` | | 🔶 |
| FB 数组元素调用 | `aObjects[2]();` | | 🔶 |
| 赋值作表达式（ExST） | `IF b := (i = 1) THEN ... END_IF` | | 🔶 |

## 2. 表达式与运算符（16.1.3.3 / 16.3）

绑定强度：`( )` > 函数调用 > EXPT/一元 -/NOT > `* / MOD` > `+ -` > 比较 `= <> < > <= >=` > AND/AND_THEN > XOR > OR/OR_ELSE。

| 构造 | 示例 | 支持度 |
|---|---|---|
| 算术 `+ - * / MOD EXPT MOVE` | `n := 9 MOD 2;` `f := EXPT(7, 2);` | ✅ |
| 逻辑 `AND OR XOR NOT AND_THEN OR_ELSE` | `IF p <> 0 AND_THEN p^ = 99 THEN` | ✅ |
| 比较 `= <> < > <= >=` | `b := 40 <> 40;` | ✅ |
| 移位 `SHL SHR ROL ROR` | `nRes := SHL(nIn, 2);` | ✅ |
| 位串 `AND OR XOR NOT`（位级） | `n := 2#1001_0011 AND 2#1000_1010;` | ✅ |
| 选择 `SEL MAX MIN LIMIT MUX` | `n := LIMIT(30, 90, 80);` | ✅ |
| 数学 `ABS SQRT LN LOG EXP SIN COS TAN ASIN ACOS ATAN` | `f := SQRT(16);` | ✅ |
| 地址 `ADR` `BITADR`、解引用 `^` | `p := ADR(nVar);` `n := p^;` | ✅ |
| `SIZEOF XSIZEOF INDEXOF` | `n := SIZEOF(aArr);` | 🔶 |
| 类型转换 `*_TO_*` / `TO_*` / `TO___UXINT`（三下划线） | `n := INT_TO_SINT(42);` `TO___UXINT(n);` | 🔶 |
| `TRUNC` `TRUNC_INT` | `n := TRUNC(1.9);` | 🔶 |
| `__NEW` / `__DELETE` | `pDut := __NEW(ST_S);` `__DELETE(pDut);` | 🔶 |
| `__ISVALIDREF` | `b := __ISVALIDREF(refInt);` | 🔶 |
| `__QUERYINTERFACE` / `__QUERYPOINTER` | `b := __QUERYINTERFACE(iBase, iSub);` | 🔶 |
| `__VARINFO` `__POUNAME` `__POSITION` | `s := __POSITION();` | 🔶 |
| `TIME()`（系统函数） | `t := TIME();` | 🔶 |
| 命名空间 `GVL.var`、库 `lib.fb(...)`、枚举 `E_C.eBlue` | | ✅ |
| 前导点全局 `.nGlobVar` | `n := .nGlobVar1;` | ⚠️ 语句开头走 verbatim |
| `__TRY/__CATCH/__FINALLY/__ENDTRY` | | ⚠️ 未专门解析（无块缩进） |
| 位访问 `nVarA.2` / `nVar.cBit` / BIT 成员 | `nVarA.2 := bVarB;` | 🔶 |

## 3. 字面量（16.4）

| 构造 | 示例 | 支持度 |
|---|---|---|
| BOOL | `TRUE` / `FALSE` | ✅ |
| 整数（十进制含 `_`） | `n := 1_000_000;` | ✅ |
| 基数 `2#` `8#` `16#` | `n := 2#1001_0011;` `n := 16#FFFF_FFFF;` | ✅ |
| 类型前缀+基数组合 | `DINT#16#A1` | ✅（TypedLiteral） |
| REAL/LREAL（点/指数/负指数） | `r := 7.4;` `r := 1.64e+009;` `r := -1E-44;` | ✅ |
| 类型字面量（一般形式） | `DINT#34;` `BOOL#TRUE;` `REAL#3.14;`（类型须大写） | ✅ |
| STRING `'...'` | `s := 'Hello';` | ✅ |
| WSTRING `"..."` | `ws := "wide";` | ✅ |
| `$` 转义（hex/控制/`$$`/`$'`） | `s := 'a$Lb$Tc$$d$'e$41';` | ✅ |
| TIME `T#` / `TIME#` | `T#14ms;` `TIME#12h34m15s;` | ✅ |
| LTIME | `LTIME#1000d15h23m12s34ms2us44ns;` | ✅ |
| DATE `DATE#`/`D#` | `DATE#2018-8-8;` `D#2018-8-31;` | ✅ |
| LDATE `LDATE#`/`LD#` | `LDATE#2018-8-8;` | ✅ |
| DT `DATE_AND_TIME#`/`DT#`（秒可小数） | `DT#2018-08-08-13:33:20.5;` | ✅ |
| LDT `LDATE_AND_TIME#`/`LDT#` | ⚠️ **全称 `LDATE_AND_TIME#` 前缀表缺失**（`LDT#` 可用） |
| TOD `TIME_OF_DAY#`/`TOD#` | `TOD#12:34:56.789;` | ✅ |
| LTOD `LTIME_OF_DAY#`/`LTOD#` | ⚠️ **全称 `LTIME_OF_DAY#` 前缀表缺失**（`LTOD#` 可用） |
| 直接地址 `%IX0.0 %QW0 %MW10 %I*`（含无大小前缀 `%Q7.5`、多级点分 `%IW2.5.7.1`、无空格 `AT%I*`） | `b AT %I* : BOOL;` | ✅ |

## 4. 变量声明（16.2）

| 构造 | 示例 | 支持度 |
|---|---|---|
| VAR / VAR_INPUT / VAR_OUTPUT / VAR_IN_OUT / VAR_GLOBAL / VAR_TEMP / VAR_STAT / VAR_EXTERNAL / VAR_INST | | ✅ |
| VAR_IN_OUT CONSTANT | `VAR_IN_OUT CONSTANT c : STRING(16); END_VAR` | 🔶 |
| CONSTANT（VAR/VAR_INPUT/VAR_STAT/VAR_GLOBAL + CONSTANT） | `VAR CONSTANT c : REAL := 1.19; END_VAR` | ✅ |
| VAR_GENERIC CONSTANT（泛型） | `VAR_GENERIC CONSTANT n : UDINT := 1; END_VAR` | ⚠️ 关键词表缺失 |
| 泛型实例化 `FB_X<100>` / `FB_X<(2*c)>` / EXTENDS 泛型 | `fb : FB_Sample<100>;` | ⚠️ EXTENDS 位置不支持；声明位置未验证 |
| RETAIN / PERSISTENT（含 VAR_GLOBAL PERSISTENT、组合修饰） | `VAR RETAIN n : INT; END_VAR` | ✅ |
| 成员访问修饰 PUBLIC/PRIVATE/PROTECTED/INTERNAL | `PUBLIC n : INT;` | ✅（1.8.8） |
| AT 绑定（含双 AT、`AT%I*` 无空格） | `b AT %IX2.3 : BOOL;` | ✅ |
| 点前缀全局访问 | `n := .nGlob;` | ⚠️（同运算符表） |
| SUPER / THIS | `SUPER^();` `THIS^.nVar := 2;` | ⚠️ **语句开头未分派**（表达式中间可用） |
| 逗号多声明 | `a, b, c : INT;` | ✅ |
| 初始化（:= 表达式 / 括号结构初始化 / 数组 `[...]` / FB_init 参数） | `st : ST_X := (n1 := 1);` | 🔶 |

## 5. 数据类型 / DUT（16.5）

| 构造 | 示例 | 支持度 |
|---|---|---|
| 标准类型全集（含 XINT/UXINT/XWORD/PVOID/ANY/ANY_*） | `x : __UXINT;` `in : ANY;` | ✅（Identifier 处理） |
| 子范围内联 | `nVarA : INT(-4095..4095);` | 🔶（括号初始化路径收编） |
| BIT | `b : BIT;` | 🔶 |
| STRING 定长 `STRING(n)` / `STRING[n]` | | ✅ |
| 指针 `POINTER TO T`（含 `POINTER TO STRING(n)`、指针索引 `p[i]`） | | ✅ |
| 引用 `REFERENCE TO T` | | ✅ |
| 接口变量 / `i := fb;` / 接口方法调用 | `IF ip <> 0 THEN n := ip.Add(3,6);` | 🔶 |
| 数组全家族（多维、`ARRAY[*]`、初始化 `[2(10)]`、结构数组、FB 数组、数组的数组 `a[i][j]`、常量边界） | `a : ARRAY[*,*] OF INT;` | 🔶 |
| STRUCT（含嵌套、成员注释、STRUCT RETAIN/PERSISTENT） | | ✅ |
| STRUCT EXTENDS | `TYPE ST_Sub EXTENDS ST_Base : STRUCT ...` | ⚠️ 未知 body 分支 |
| 枚举（单行/多行/带值/基类型/pragma qualified_only+strict/to_string） | `(eRed, eGreen := 10) DWORD;` | ✅ |
| 枚举默认组件初始化 | `) DWORD := eBlack;` | ⚠️ `:=` 后部分游离 |
| 别名 `TYPE T : <type>; END_TYPE` | | ✅ |
| UNION / UNION RETAIN | | ✅ |
| `__SYSTEM.*`（ExceptionCode/TYPE_CLASS/VAR_INFO） | `exc : __SYSTEM.ExceptionCode;` | 🔶 |
| `FB_init/FB_reinit/FB_exit` 特殊方法 | | 🔶 |

## 6. 编译指示（16.8.2）

词法上整个 `{...}` 是单个 Pragma token。已支持的位置：POU/TYPE/GVL 头上方、VAR 块内变量行上方、语句之间。
常用 attribute：`'reflection' 'hide' 'hide_all_locals' 'noinit'/'no_init'/'no-init' 'init_on_onlchange' 'pack_mode' 'displaymode' 'conditionalshow' 'dataflow' 'linkalways' 'obsolete' 'enable_dynamic_creation' 'global_init_slot' 'call_after_init' 'qualified_only' 'strict' 'to_string' 'TcEncoding' 'no_copy' 'no_check'` 等；取值形式 `'x'`、`'x' := 'v'`、多值 `'a, b'`；非 attribute 裸 pragma：`{noflow}` `{flow}`。
支持度：✅（位置与取值形式均有路径）；变量行上带 `;` 的写法 `{attribute 'DoCount'};` 🔶 需用例。

## 7. 已知不支持清单（底线：等价性校验必须通过）

1. `__TRY/__CATCH/__FINALLY/__ENDTRY` 块（16.3.11.6）
2. `TYPE X EXTENDS Y : STRUCT ...`（16.5.17）
3. `VAR_GENERIC CONSTANT` 与 `EXTENDS FB_X<100>`（16.2.11）
4. 枚举默认组件初始化 `) DWORD := eBlack;`（16.5.18）
5. 语句开头的 `THIS^.` / `SUPER^.` / 前导点 `.nGlob`（16.2.13/14、16.3.7.1）
6. `LDATE_AND_TIME#` / `LTIME_OF_DAY#` 全称前缀（16.4.6）

这些构造的**排版**可能劣化（verbatim/未知 body），但格式化**绝不许**丢 token、丢注释、破坏可重解析性——由 `FormattingValidator` 与 `STSyntaxSpec` 的底线测试强制。
