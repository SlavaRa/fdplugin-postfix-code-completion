using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using AS2Context;
using ASCompletion.Completion;
using ASCompletion.Context;
using ASCompletion.Model;
using PluginCore;
using PluginCore.Helpers;
using ProjectManager.Projects.Haxe;
using ScintillaNet;

namespace PostfixCodeCompletion.Completion
{
    internal delegate void HaxeCompleteResultHandler<in T>(HaxeComplete hc, T result, HaxeCompleteStatus status);

    interface IHaxeCompletionHandler
    {
        string GetCompletion(string[] args);
        void Stop();
    }

    internal class HaxeComplete
    {
        static readonly Regex reArg = new Regex("^(-cp|-resource)\\s*([^\"'].*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex reMacro = new Regex("^(--macro)\\s*([^\"'].*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public readonly ScintillaControl Sci;
        public readonly ASResult Expr;
        public readonly string CurrentWord;
        public readonly bool AutoHide;
        public readonly HaxeCompilerService CompilerService;
        public HaxeCompleteStatus Status;
        public string Errors;
        HaxeCompleteResult result;
        readonly IHaxeCompletionHandler handler;
        readonly string tempFileName;

        public HaxeComplete(ScintillaControl sci, ASResult expr, bool autoHide, IHaxeCompletionHandler completionHandler, HaxeCompilerService compilerService)
        {
            Sci = sci;
            Expr = expr;
            CurrentWord = Sci.GetWordFromPosition(Sci.CurrentPos);
            AutoHide = autoHide;
            handler = completionHandler;
            CompilerService = compilerService;
            Status = HaxeCompleteStatus.None;
            tempFileName = GetTempFileName();
        }

        static string GetTempFileName()
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), "FlashDevelop");
            var projDirectoryName = Path.GetDirectoryName(PluginBase.CurrentProject.ProjectPath);
            projDirectoryName = Path.GetDirectoryName(projDirectoryName);
            return PluginBase.MainForm.CurrentDocument.FileName.Replace(projDirectoryName, tempFolder);
        }

        public void GetPositionType(HaxeCompleteResultHandler<HaxeCompleteResult> callback)
        {
            var pos = Sci.CurrentPos;
            Sci.SetSel(pos, Expr.Context.Position);
            var selText = Sci.SelText;
            Sci.ReplaceSel(string.Empty);
            var directoryName = Path.GetDirectoryName(tempFileName);
            if (!Directory.Exists(directoryName)) Directory.CreateDirectory(directoryName);
            FileHelper.WriteFile(tempFileName, Sci.Text, Sci.Encoding, false);
            if (!string.IsNullOrEmpty(selText))
            {
                pos = Expr.Context.Position;
                Sci.SetSel(pos, pos);
                Sci.ReplaceSel(selText);
            }
            PluginBase.MainForm.CallCommand("Save", null);
            result = new HaxeCompleteResult();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Status = ParseLines(handler.GetCompletion(BuildHxmlArgs()));
                Notify(callback, result);
            });
        }

        void Notify<T>(HaxeCompleteResultHandler<T> callback, T result)
        {
            if (Sci.InvokeRequired)
            {
                Sci.BeginInvoke((MethodInvoker) delegate { Notify(callback, result); });
                return;
            }
            callback(this, result, Status);
        }

        string[] BuildHxmlArgs()
        {
            if (PluginBase.CurrentProject == null || !(PluginBase.CurrentProject is HaxeProject)
                || !(ASContext.Context is Context))
                return null;
            var project = (HaxeProject) PluginBase.CurrentProject;
            var pos = GetDisplayPosition();
            var paths = ProjectManager.PluginMain.Settings.GlobalClasspaths.ToArray();
            var args = new List<string>(project.BuildHXML(paths, "Nothing__", true))
            {
                $"-cp {Path.GetDirectoryName(tempFileName)}"
            };
            RemoveComments(args);
            QuotePath(args);
            EscapeMacros(args);
            var mode = "";
            if (CompilerService == HaxeCompilerService.Type) mode = "@type";
            args.Insert(0, $"--display {tempFileName}@{pos}{mode}");
            args.Insert(1, "-D use_rtti_doc");
            args.Insert(2, "-D display-details");
            if (project.TraceEnabled) args.Insert(2, "-debug");
            return args.ToArray();
        }

        static void RemoveComments(IList<string> args)
        {
            for (var i = 0; i < args.Count; i++)
            {
                var arg = args[i];
                if (string.IsNullOrEmpty(arg)) continue;
                if (arg.StartsWith('#')) // commented line
                    args[i] = "";
            }
        }

        static void QuotePath(IList<string> args)
        {
            for (var i = 0; i < args.Count; i++)
            {
                var arg = args[i];
                if (string.IsNullOrEmpty(arg)) continue;
                var m = reArg.Match(arg);
                if (m.Success) args[i] = $"{m.Groups[1].Value} \"{m.Groups[2].Value.Trim()}\"";
            }
        }

        static void EscapeMacros(IList<string> args)
        {
            for (var i = 0; i < args.Count; i++)
            {
                var arg = args[i];
                if (string.IsNullOrEmpty(arg)) continue;
                var m = reMacro.Match(arg);
                if (m.Success) args[i] = $"{m.Groups[1].Value} \"{m.Groups[2].Value.Trim()}\"";
            }
        }

        int GetDisplayPosition()
        {
            return CompilerService switch
            {
                HaxeCompilerService.Type => Expr.Context.Position + 1,
                _ => Expr.Context.Position
            };
        }

        HaxeCompleteStatus ParseLines(string lines)
        {
            if (!lines.StartsWith("<"))
            {
                Errors = lines.Trim();
                return HaxeCompleteStatus.Error;
            }
            try
            {
                using var stream = new StringReader(lines);
                using var reader = new XmlTextReader(stream);
                return ProcessResponse(reader);
            }
            catch (Exception ex)
            {
                Errors = "PPC: Error parsing Haxe compiler output: " + ex.Message;
                return HaxeCompleteStatus.Error;
            }
        }

        HaxeCompleteStatus ProcessResponse(XmlReader reader)
        {
            reader.MoveToContent();
            switch (reader.Name)
            {
                case "type":
                    ProcessType(reader);
                    return HaxeCompleteStatus.Type;
            }
            return HaxeCompleteStatus.Failed;
        }

        void ProcessType(XmlReader reader)
        {
            var parts = Expr.Context.Value.Split('.');
            var name = parts[parts.Length - 1];
            var type = new MemberModel {Name = name};
            ExtractType(reader, type);
            result.Type = type;
        }

        static void ExtractType(XmlReader reader, MemberModel member)
        {
            var type = ReadValue(reader);
            if (string.IsNullOrEmpty(type))
            {
                if (member.Flags != 0) return;
                member.Flags = char.IsLower(member.Name[0]) ? FlagType.Package : FlagType.Class;
            }
            else
            {
                var types = type.Split(new[] { "->" }, StringSplitOptions.RemoveEmptyEntries);
                if (types.Length > 1)
                {
                    member.Flags = FlagType.Function;
                    member.Parameters = new List<MemberModel>();
                    for (var i = 0; i < types.Length - 1; i++)
                    {
                        var param = new MemberModel(types[i].Trim(), string.Empty, FlagType.ParameterVar, Visibility.Public);
                        member.Parameters.Add(param);
                    }
                    member.Type = types[types.Length - 1].Trim();
                }
                else
                {
                    if (member.Flags == 0) member.Flags = FlagType.Variable;
                    member.Type = type;
                }
            }
        }

        static string ReadValue(XmlReader reader)
        {
            if (reader.IsEmptyElement) return string.Empty;
            reader.Read();
            return reader.Value.Trim();
        }
    }

    enum HaxeCompleteStatus
    {
        None = 0,
        Failed = 1,
        Error = 2,
        Type = 3
    }

    enum HaxeCompilerService
    {
        Type
    }

    class HaxeCompleteResult
    {
        public MemberModel Type;
    }
}