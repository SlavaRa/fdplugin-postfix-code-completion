using System.IO;
using PluginCore;
using PluginCore.Controls;
using PluginCore.Helpers;
using PluginCore.Managers;
using PluginCore.Utilities;
using PostfixCodeCompletion.Completion;
using PostfixCodeCompletion.Helpers;
using ProjectManager;
using ScintillaNet;

namespace PostfixCodeCompletion
{
    public class PluginMain : IPlugin
    {
        string settingFilename;

        #region Required Properties

        public int Api => 1;

        public string Name => "PostfixCodeCompletion";

        public string Guid => "21d9ab3e-93e4-4460-9298-c62f87eed7ba";

        public string Help => string.Empty;

        public string Author => "SlavaRa";

        public string Description => "Postfix code completion helps reduce backward caret jumps as you write code";

        public object Settings { get; private set; }

        #endregion

        /// <summary>
        /// Initializes the plugin
        /// </summary>
        public void Initialize()
        {
            InitBasics();
            LoadSettings();
            TemplateUtils.Settings = (Settings)Settings;
            CompleteHelper.Settings = (Settings)Settings;
            AddEventHandlers();
        }

        /// <summary>
        /// Disposes the plugin
        /// </summary>
        public void Dispose()
        {
            Complete.Stop();
            SaveSettings();
        }

        /// <summary>
        /// Handles the incoming events
        /// </summary>
        public void HandleEvent(object sender, NotifyEvent e, HandlingPriority priority)
        {
            switch (e.Type)
            {
                case EventType.Command:
                    if (((DataEvent) e).Action == ProjectManagerEvents.Project) Complete.Start();
                    break;
                case EventType.Keys:
                    e.Handled = Complete.OnShortcut(((KeyEvent) e).Value);
                    break;
            }
        }

        /// <summary>
        /// Initializes important variables
        /// </summary>
        void InitBasics()
        {
            var path = Path.Combine(PathHelper.DataDir, Name);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            settingFilename = Path.Combine(path, "Settings.fdb");
        }

        /// <summary>
        /// Loads the plugin settings
        /// </summary>
        void LoadSettings()
        {
            Settings = new Settings();
            if (!File.Exists(settingFilename)) SaveSettings();
            else Settings = (Settings) ObjectSerializer.Deserialize(settingFilename, Settings);
        }

        /// <summary>
        /// Adds the required event handlers
        /// </summary>
        void AddEventHandlers()
        {
            EventManager.AddEventHandler(this, EventType.Command);
            EventManager.AddEventHandler(this, EventType.Keys, HandlingPriority.High);
            UITools.Manager.OnCharAdded += OnCharAdded;
        }

        /// <summary>
        /// Saves the plugin settings
        /// </summary>
        void SaveSettings() => ObjectSerializer.Serialize(settingFilename, Settings);

        static void OnCharAdded(ScintillaControl sender, int value) => Complete.OnCharAdded(value);
    }
}