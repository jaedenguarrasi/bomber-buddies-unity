using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace BomberBuddies.Editor
{
    /// <summary>iOS needs to be told why the game looks for buddies on the local network (Nearby play), or the
    /// Wi-Fi beacon is silently blocked. Edits the Xcode project's Info.plist as plain text so no iOS module is needed.</summary>
    public static class IosPlist
    {
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plist = Path.Combine(path, "Info.plist");
            if (!File.Exists(plist)) return;
            string s = File.ReadAllText(plist);
            if (s.Contains("NSLocalNetworkUsageDescription")) return;
            string add = "\t<key>NSLocalNetworkUsageDescription</key>\n\t<string>Bomber Buddies looks for buddies hosting a game on your Wi-Fi so you can fly together without the internet.</string>\n" +
                         "\t<key>NSBonjourServices</key>\n\t<array>\n\t\t<string>_bomberbuddies._udp</string>\n\t</array>\n";
            int i = s.LastIndexOf("</dict>");
            if (i < 0) return;
            File.WriteAllText(plist, s.Substring(0, i) + add + s.Substring(i));
            Debug.Log("Info.plist: added the local network usage description for Nearby play");
        }
    }
}
