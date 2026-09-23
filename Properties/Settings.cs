using System.Configuration;

namespace DiskTester.Properties
{
    internal sealed partial class Settings : ApplicationSettingsBase
    {
        private static Settings defaultInstance = ((Settings)(Synchronized(new Settings())));

        public static Settings Default
        {
            get { return defaultInstance; }
        }

        [UserScopedSetting()]
        [DefaultSettingValue("")]
        public string HistoryJson
        {
            get { return ((string)(this["HistoryJson"])); }
            set { this["HistoryJson"] = value; }
        }
    }
}
