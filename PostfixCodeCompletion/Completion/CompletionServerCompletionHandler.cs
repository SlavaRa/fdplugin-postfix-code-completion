using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using PluginCore;
using PluginCore.Managers;
using ProjectManager.Projects.Haxe;

namespace PostfixCodeCompletion.Completion
{
    delegate void FallbackNeededHandler(bool notSupported);

    class CompletionServerCompletionHandler : IHaxeCompletionHandler
    {
        public event FallbackNeededHandler FallbackNeeded;

        readonly Process haxeProcess;
        readonly int port;
        bool listening;
        bool failure;

        public CompletionServerCompletionHandler(Process haxeProcess, int port)
        {
            this.haxeProcess = haxeProcess;
            this.port = port;
            Environment.SetEnvironmentVariable("HAXE_SERVER_PORT", port.ToString());
        }

        public bool IsRunning()
        {
            try { return !haxeProcess.HasExited; } 
            catch { return false; }
        }

        /// <summary>
        /// Allows an object to try to free resources and perform other cleanup operations before it is reclaimed by garbage collection.
        /// </summary>
        ~CompletionServerCompletionHandler()
        {
            Stop();
        }

        public string GetCompletion(string[] args)
        {
            if (args == null || haxeProcess == null) return string.Empty;
            if (!IsRunning()) StartServer();
            try
            {
                var client = new TcpClient("127.0.0.1", port);
                var writer = new StreamWriter(client.GetStream());
                writer.WriteLine("--cwd " + ((HaxeProject) PluginBase.CurrentProject).Directory);
                foreach (var arg in args)
                    writer.WriteLine(arg);
                writer.Write("\0");
                writer.Flush();
                var reader = new StreamReader(client.GetStream());
                var lines = reader.ReadToEnd();
                client.Close();
                return lines;
            }
            catch(Exception ex)
            {
                TraceManager.AddAsync(ex.Message);
                if (!failure) FallbackNeeded?.Invoke(false);
                failure = true;
                return string.Empty;
            }
        }

        public void StartServer()
        {
            if (haxeProcess == null || IsRunning()) return;
            haxeProcess.Start();
            if (listening) return;
            listening = true;
            haxeProcess.BeginOutputReadLine();
            haxeProcess.BeginErrorReadLine();
            haxeProcess.OutputDataReceived += OnOutputDataReceived;
            haxeProcess.ErrorDataReceived += OnErrorDataReceived;
        }

        static void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            TraceManager.AddAsync("PCC: " + e.Data, 2);
        }

        void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null || !Regex.IsMatch(e.Data, "Error.*--wait")) return;
            if (!failure) FallbackNeeded?.Invoke(true);
            failure = true;
        }

        public void Stop()
        {
            if (IsRunning()) haxeProcess.Kill();
        }
    }
}