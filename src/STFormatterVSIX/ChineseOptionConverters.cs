using System;
using System.ComponentModel;
using System.Globalization;
using STFormatterCore.Configuration;

namespace STFormatterVSIX
{
    // 选项页的下拉框默认显示 enum 成员名和 True/False，这些是英文。下面三个
    // 转换器把取值显示成中文。DialogPage 用 TypeConverter 读写设置，所以
    // ConvertFrom 在中文标签之外仍然接受英文枚举名 / True / False，已保存的
    // 旧设置不会因为界面改中文而读不回来。

    /// <summary>
    /// 布尔选项显示为「是 / 否」。
    /// </summary>
    internal sealed class ChineseBooleanConverter : BooleanConverter
    {
        private const string Yes = "是";
        private const string No = "否";

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            string text = value as string;
            if (text != null)
            {
                text = text.Trim();
                if (string.Equals(text, Yes, StringComparison.Ordinal)) return true;
                if (string.Equals(text, No, StringComparison.Ordinal)) return false;
            }
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is bool)
                return (bool)value ? Yes : No;
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    /// <summary>
    /// 换行符选项显示为「自动 / Windows (CRLF) / Unix (LF)」。
    /// </summary>
    internal sealed class ChineseLineEndingConverter : EnumConverter
    {
        public ChineseLineEndingConverter() : base(typeof(LineEnding))
        {
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            string text = value as string;
            if (text != null)
            {
                switch (text.Trim())
                {
                    case "自动":
                    case "自动（跟随原文件）":
                        return LineEnding.Auto;
                    case "Windows":
                    case "Windows (CRLF)":
                        return LineEnding.CRLF;
                    case "Unix":
                    case "Unix (LF)":
                        return LineEnding.LF;
                }
            }
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is LineEnding)
            {
                switch ((LineEnding)value)
                {
                    case LineEnding.Auto: return "自动（跟随原文件）";
                    case LineEnding.CRLF: return "Windows (CRLF)";
                    case LineEnding.LF: return "Unix (LF)";
                }
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    /// <summary>
    /// 类型大小写选项显示为「全部大写 / 全部小写 / 保持原样」。
    /// </summary>
    internal sealed class ChineseTypeCaseConverter : EnumConverter
    {
        public ChineseTypeCaseConverter() : base(typeof(TypeCase))
        {
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            string text = value as string;
            if (text != null)
            {
                switch (text.Trim())
                {
                    case "全部大写":
                    case "大写":
                        return TypeCase.Upper;
                    case "全部小写":
                    case "小写":
                        return TypeCase.Lower;
                    case "保持原样":
                    case "保持":
                        return TypeCase.Preserve;
                }
            }
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is TypeCase)
            {
                switch ((TypeCase)value)
                {
                    case TypeCase.Upper: return "全部大写";
                    case TypeCase.Lower: return "全部小写";
                    case TypeCase.Preserve: return "保持原样";
                }
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
