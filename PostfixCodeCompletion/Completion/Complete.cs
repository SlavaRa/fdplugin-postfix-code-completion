using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ASCompletion.Completion;
using ASCompletion.Context;
using ASCompletion.Model;
using HaXeContext;
using PluginCore;
using PluginCore.Controls;
using PluginCore.Managers;
using PluginCore.Utilities;
using PostfixCodeCompletion.Helpers;
using ProjectManager.Projects.AS3;
using ProjectManager.Projects.Haxe;
using TemplateUtils = PostfixCodeCompletion.Helpers.TemplateUtils;

namespace PostfixCodeCompletion.Completion
{
    public class PCCCompleteFactory
    {
        public IPCCComplete CreateComplete()
        {
            switch (PluginBase.CurrentProject)
            {
                case AS3Project _: return new PCCASComplete();
                case HaxeProject _: return new PCCHaxeComplete();
                default: return new PCCComplete();
            }
        }
    }

    public class Complete
    {
        public static PCCCompleteFactory Factory = new PCCCompleteFactory();
        static IPCCComplete current;

        internal static void Start()
        {
            current?.Stop();
            current = Factory.CreateComplete();
            current.Start();
        }

        internal static void Stop()
        {
            current?.Stop();
            current = null;
        }

        internal static bool OnShortcut(Keys keys) => current?.OnShortcut(keys) ?? false;

        internal static bool OnCharAdded(int value) => current?.OnCharAdded(value) ?? false;

        internal static void UpdateCompletionList(IList<ICompletionListItem> options) => current?.UpdateCompletionList(options);

        internal static void UpdateCompletionList(ASResult expr, IList<ICompletionListItem> options) => current?.UpdateCompletionList(expr, options);

        internal static void UpdateCompletionList(MemberModel target, ASResult expr, IList<ICompletionListItem> options) => current?.UpdateCompletionList(target, expr, options);
    }

    public interface IPCCComplete
    {
        void Start();
        void Stop();
        bool OnShortcut(Keys keys);
        bool OnCharAdded(int value);
        void UpdateCompletionList(IList<ICompletionListItem> options);
        bool UpdateCompletionList(ASResult expr, IList<ICompletionListItem> options);
        void UpdateCompletionList(MemberModel target, ASResult expr, IList<ICompletionListItem> options);
    }

    public class PCCComplete : IPCCComplete
    {
        public virtual void Start()
        {
        }

        public virtual void Stop()
        {
        }

        public virtual bool OnShortcut(Keys keys) => false;

        public virtual bool OnCharAdded(int value) => false;

        public virtual void UpdateCompletionList(IList<ICompletionListItem> options) => UpdateCompletionList(CompleteHelper.GetCurrentExpressionType(), options);

        public virtual bool UpdateCompletionList(ASResult expr, IList<ICompletionListItem> options) => false;

        public virtual void UpdateCompletionList(MemberModel target, ASResult expr, IList<ICompletionListItem> options)
        {
        }
    }

    public class PCCASComplete : PCCComplete, IEventHandler
    {
        public override void Start() => EventManager.AddEventHandler(this, EventType.Command);

        public override void Stop() => EventManager.RemoveEventHandler(this);

        public override bool OnShortcut(Keys keys)
        {
            if (CompletionList.Active || keys != (Keys.Control | Keys.Space)) return false;
            var expr = CompleteHelper.GetCurrentExpressionType();
            if (expr == null || expr.IsNull()) return false;
            var result = ASComplete.OnShortcut(keys, PluginBase.MainForm.CurrentDocument.SciControl);
            UpdateCompletionList(expr, new List<ICompletionListItem>());
            return result;
        }

        public override bool OnCharAdded(int value)
        {
            if (CompletionList.Active) return false;
            if ((char) value != '.' || !TemplateUtils.GetHasTemplates()) return false;
            var sci = PluginBase.MainForm.CurrentDocument.SciControl;
            if (sci.PositionIsOnComment(sci.CurrentPos)) return false;
            if (ASComplete.OnChar(sci, value, false) || Reflector.ASComplete.HandleDotCompletion(sci, true)) return false;
            var expr = CompleteHelper.GetCurrentExpressionType();
            if (expr == null || expr.IsNull()) return false;
            UpdateCompletionList(expr, new List<ICompletionListItem>());
            return true;
        }

        public override bool UpdateCompletionList(ASResult expr, IList<ICompletionListItem> options)
        {
            if (expr == null || expr.IsNull()) return false;
            var target = CompleteHelper.GetCompletionTarget(expr);
            if (target == null) return false;
            UpdateCompletionList(target, expr, options);
            return true;
        }

        public override void UpdateCompletionList(MemberModel target, ASResult expr, IList<ICompletionListItem> options)
        {
            if (target == null || !TemplateUtils.GetHasTemplates()) return;
            var labels = new HashSet<string>();
            foreach (var item in options)
            {
                if (item is PostfixCompletionItem) labels.Add(item.Label);
            }
            var items = CompleteHelper.GetCompletionItems(target, expr);
            foreach (var item in items)
            {
                if (!labels.Contains(item.Label)) options.Add(item);
            }
            if (CompletionList.Active) return;
            var sci = PluginBase.MainForm.CurrentDocument.SciControl;
            var word = sci.GetWordLeft(sci.CurrentPos - 1, false);
            if (!string.IsNullOrEmpty(word))
            {
                options = options.Where(it =>
                {
                    var score = CompletionList.SmartMatch(it.Label, word, word.Length);
                    return score > 0 && score < 6;
                }).ToList();
            }
            CompletionList.Show((List<ICompletionListItem>) options, false, word);
        }

        public void HandleEvent(object sender, NotifyEvent e, HandlingPriority priority)
        {
            switch (e.Type)
            {
                case EventType.Command:
                    var de = (DataEvent)e;
                    if (de.Action == "ASCompletion.DotCompletion") UpdateCompletionList((IList<ICompletionListItem>) de.Data);
                    break;
            }
        }
    }

    public class PCCHaxeComplete : PCCASComplete
    {
        IHaxeCompletionHandler completionModeHandler;

        public override void Start()
        {
            base.Start();
            var context = (Context) ASContext.GetLanguageContext("haxe");
            if (context == null) return;
            var settings = (HaXeSettings) context.Settings;
            settings.CompletionModeChanged -= OnHaxeCompletionModeChanged;
            settings.CompletionModeChanged += OnHaxeCompletionModeChanged;
            OnHaxeCompletionModeChanged();
        }

        public override void Stop()
        {
            base.Stop();
            completionModeHandler?.Stop();
            completionModeHandler = null;
        }

        public override bool UpdateCompletionList(ASResult expr, IList<ICompletionListItem> options)
        {
            var result = base.UpdateCompletionList(expr, options);
            if (result) return true;
            if (expr == null || expr.IsNull() || expr.IsPackage || expr.Context == null || completionModeHandler == null) return false;
            var sci = PluginBase.MainForm.CurrentDocument.SciControl;
            if (sci.CharAt(expr.Context.Position) != '.') return false;
            var hc = new HaxeComplete(sci, expr, false, completionModeHandler, HaxeCompilerService.Type);
            hc.GetPositionType(OnFunctionTypeResult);
            return true;
        }

        void OnHaxeCompletionModeChanged()
        {
            if (completionModeHandler != null)
            {
                completionModeHandler.Stop();
                completionModeHandler = null;
            }
            if (!(PluginBase.CurrentProject is HaxeProject)) return;
            var settings = (HaXeSettings) ((Context) ASContext.GetLanguageContext("haxe")).Settings;
            var sdk = settings.InstalledSDKs.FirstOrDefault(it => it.Path == PluginBase.CurrentProject.CurrentSDK);
            if (sdk == null || new SemVer(sdk.Version).IsOlderThan(new SemVer("3.2.0"))) return;
            switch (settings.CompletionMode)
            {
                case HaxeCompletionModeEnum.CompletionServer:
                    if (settings.CompletionServerPort < 1024) completionModeHandler = new CompilerCompletionHandler(CreateHaxeProcess(string.Empty));
                    else
                    {
                        completionModeHandler = new CompletionServerCompletionHandler(
                            CreateHaxeProcess($"--wait {settings.CompletionServerPort}"),
                            settings.CompletionServerPort
                        );
                        ((CompletionServerCompletionHandler) completionModeHandler).FallbackNeeded += OnHaxeContextFallbackNeeded;
                    }
                    break;
                default:
                    completionModeHandler = new CompilerCompletionHandler(CreateHaxeProcess(string.Empty));
                    break;
            }
        }

        void OnHaxeContextFallbackNeeded(bool notSupported)
        {
            TraceManager.AddAsync("PCC: This SDK does not support server mode");
            completionModeHandler?.Stop();
            completionModeHandler = new CompilerCompletionHandler(CreateHaxeProcess(string.Empty));
        }

        void OnFunctionTypeResult(HaxeComplete hc, HaxeCompleteResult result, HaxeCompleteStatus status)
        {
            switch (status)
            {
                case HaxeCompleteStatus.Error:
                    TraceManager.AddAsync(hc.Errors, -3);
                    if (hc.AutoHide) CompletionList.Hide();
                    break;
                case HaxeCompleteStatus.Type:
                    var expr = hc.Expr;
                    if (result.Type is ClassModel)
                    {
                        expr.Type = (ClassModel) result.Type;
                        expr.Member = null;
                        UpdateCompletionList(expr.Type, expr, Reflector.CompletionList.AllItems);
                    }
                    else
                    {
                        expr.Type = ASContext.Context.ResolveType(result.Type.Type, result.Type.InFile);
                        expr.Member = result.Type;
                        UpdateCompletionList(expr.Member, expr, Reflector.CompletionList.AllItems);
                    }
                    break;
            }
        }

        static Process CreateHaxeProcess(string args)
        {
            var process = Path.Combine(PluginBase.CurrentProject.CurrentSDK, "haxe.exe");
            if (!File.Exists(process)) return null;
            var result = new Process
            {
                StartInfo =
                {
                    FileName = process,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                },
                EnableRaisingEvents = true
            };
            return result;
        }
    }
}