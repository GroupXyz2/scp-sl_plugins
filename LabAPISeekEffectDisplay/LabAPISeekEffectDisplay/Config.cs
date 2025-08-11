using System.ComponentModel;

namespace LabAPISeekEffectDisplay
{
    public class Config
    {
        [Description("Ob Debug-Ausgaben aktiviert werden sollen")]
        public bool DebugMode { get; set; } = false;

        [Description("Detaillierte Debug-Ausgaben für Effekt-Updates")]
        public bool VerboseEffectDebug { get; set; } = false;

        [Description("Debug-Ausgaben für Event-Handler")]
        public bool EventDebug { get; set; } = false;

        [Description("Debug-Ausgaben für Player-Tracking")]
        public bool PlayerTrackingDebug { get; set; } = false;
    }
}
