using System;
using System.Collections.Generic;
using System.IO;
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
    internal delegate void HaxeCompleteResultHandler<T>(HaxeComplete hc, T result, HaxeCompleteStatus status);

    interface IHaxeCompletionHandler
    {
        string GetCompletion(string[] args);
        void Stop();
    }

    internal class HaxeComplete
    {
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
            string tempFolder = Path.Combine(Path.GetTempPath(), "FlashDevelop");
            string projDirectoryName = Path.GetDirectoryName(PluginBase.CurrentProject.ProjectPath);
            projDirectoryName = Path.GetDirectoryName(projDirectoryName);
            return PluginBase.MainForm.CurrentDocument.FileName.Replace(projDirectoryName, tempFolder);
        }

        public void GetPositionType(HaxeCompleteResultHandler<HaxeCompleteResult> callback)
        {
            var pos = Sci.CurrentPos;
            Sci.SetSel(pos, Expr.Context.Position);
            string selText = Sci.SelText;
            Sci.ReplaceSel(string.Empty);
            string directoryName = Path.GetDirectoryName(tempFileName);
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
            var hxproj = ((HaxeProject) PluginBase.CurrentProject);
            var pos = GetDisplayPosition();
            var paths = ProjectManager.PluginMain.Settings.GlobalClasspaths.ToArray();
            var hxmlArgs = new List<string>(hxproj.BuildHXML(paths, "Nothing__", true))
            {
                "-cp " + Path.GetDirectoryName(tempFileName)
            };
            string mode = "";
            if (CompilerService == HaxeCompilerService.Type) mode = "@type";
            hxmlArgs.Insert(0, string.Format("--display {0}@{1}{2}", tempFileName, pos, mode));
            hxmlArgs.Insert(1, "-D use_rtti_doc");
            hxmlArgs.Insert(2, "-D display-details");
            if (hxproj.TraceEnabled) hxmlArgs.Insert(2, "-debug");
            return hxmlArgs.ToArray();
        }

        int GetDisplayPosition()
        {
            switch (CompilerService)
            {
                case HaxeCompilerService.Type:
                    return Expr.Context.Position + 1;
            }
            return Expr.Context.Position;
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
                using (TextReader stream = new StringReader(lines))
                {
                    using (XmlTextReader reader = new XmlTextReader(stream))
                    {
                        return ProcessResponse(reader);
                    }
                }
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
            string[] parts = Expr.Context.Value.Split('.');
            string name = parts[parts.Length - 1];
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
                string[] types = type.Split(new[] { "->" }, StringSplitOptions.RemoveEmptyEntries);
                if (types.Length > 1)
                {
                    member.Flags = FlagType.Function;
                    member.Parameters = new List<MemberModel>();
                    for (int i = 0; i < types.Length - 1; i++)
                    {
                        MemberModel param = new MemberModel(types[i].Trim(), string.Empty, FlagType.ParameterVar, Visibility.Public);
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