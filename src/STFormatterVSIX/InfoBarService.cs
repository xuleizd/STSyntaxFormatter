using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace STFormatterVSIX
{
    /// <summary>
    /// 主窗口 InfoBar 提示（对标 CSharpier VS 扩展的 InfoBarService，精简为纯文本）。
    /// 格式化失败/校验拒绝时让用户看得见原因，而不是静默吞掉；消息带引擎版本号，
    /// 「部署的还是旧版本」这类问题一眼可查。
    /// InfoBar 是锦上添花：任何失败都吞掉并降级 ActivityLog，绝不影响保存流程。
    /// 同一条消息 30 秒内不重复弹（保存十次只弹一次）。
    /// </summary>
    internal static class InfoBarService
    {
        private const int DebounceSeconds = 30;
        private static readonly Dictionary<string, DateTime> lastShown = new Dictionary<string, DateTime>();

        /// <summary>
        /// 带引擎版本号显示一条提示。必须在 UI 线程调用（调用方保证）。
        /// </summary>
        public static void Show(string message)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                string version = typeof(STFormatterPackage).Assembly.GetName().Version?.ToString(3) ?? "?";
                string fullMessage = $"ST 格式化 v{version}：{message}";

                if (lastShown.TryGetValue(message, out var time) &&
                    (DateTime.Now - time).TotalSeconds < DebounceSeconds)
                    return;
                lastShown[message] = DateTime.Now;

                if (!(Package.GetGlobalService(typeof(SVsShell)) is IVsShell shell))
                    return;
                shell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out var property);
                if (!(property is IVsInfoBarHost infoBarHost))
                    return;

                if (!(Package.GetGlobalService(typeof(SVsInfoBarUIFactory)) is IVsInfoBarUIFactory factory))
                    return;

                // The 15.0 SDK flavor here exposes the (text, icon, close) string
                // constructor; the default moniker renders without an icon.
                var model = new InfoBarModel(fullMessage, default(ImageMoniker), true);
                var element = factory.CreateInfoBar(model);
                infoBarHost.AddInfoBar(element);
            }
            catch (Exception ex)
            {
                // InfoBar must never break the save pipeline.
                try
                {
                    ActivityLog.LogWarning("STFormatter", $"InfoBar failed: {ex.Message}");
                }
                catch
                {
                }
            }
        }
    }
}
