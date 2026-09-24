using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Puts <c>android.intent.category.LAUNCHER</c> back on the main activity after the vendored
/// Oculus SDK strips it.
///
/// <para><b>Why this exists.</b> <c>Assets/Oculus/VR/Editor/OVRManifestPreprocessor.cs</c>
/// unconditionally removes <c>LAUNCHER</c> and <c>LEANBACK_LAUNCHER</c> from the main activity's
/// intent-filter and substitutes <c>android.intent.category.INFO</c> - its own comments say
/// "always remove launcher" / "always add info launcher". That is the old Oculus <i>Store</i>
/// convention, where a shipped title is hidden from the 2D Android launcher and surfaced only
/// through the VR library.</para>
///
/// <para>The side effect is that Unity's Build and Run cannot start the app. Its deploy step looks
/// for an activity with action MAIN and category LAUNCHER, does not find one, and fails with:</para>
///
/// <code>
/// DeploymentOperationFailedException: No activity in the manifest with action MAIN and
/// category LAUNCHER. Try launching the application manually on the device.
/// </code>
///
/// <para>The APK installs correctly - only the automatic launch fails - which is why the build
/// "deploys successfully" and then refuses to start.</para>
///
/// <para>This runs after <c>OVRGradleGeneration</c> (callbackOrder 99999) and re-adds the
/// category. <c>INFO</c> and <c>com.oculus.intent.category.VR</c> are deliberately left in place:
/// they are harmless, and Meta's own submission template
/// (<c>Assets/Oculus/VR/Editor/AndroidManifest.OVRSubmission.xml</c>) ships LAUNCHER alongside
/// them. Fixing it here rather than editing the vendored SDK means an SDK update cannot silently
/// revert it.</para>
/// </summary>
public class RestoreLauncherCategory : IPostGenerateGradleAndroidProject
{
    private const string MainAction = "android.intent.action.MAIN";
    private const string LauncherCategory = "android.intent.category.LAUNCHER";

    /// Must beat OVRGradleGeneration's 99999, which is what removes the category.
    public int callbackOrder => int.MaxValue;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        if (!File.Exists(manifestPath))
        {
            Debug.LogWarning($"[RestoreLauncherCategory] No manifest at {manifestPath}; skipping.");
            return;
        }

        var doc = new XmlDocument();
        doc.Load(manifestPath);

        var manifest = doc.SelectSingleNode("/manifest") as XmlElement;
        if (manifest == null)
        {
            Debug.LogError("[RestoreLauncherCategory] No <manifest> element; leaving the file alone.");
            return;
        }

        string androidNs = manifest.GetAttribute("xmlns:android");
        if (string.IsNullOrEmpty(androidNs))
        {
            Debug.LogError("[RestoreLauncherCategory] No android namespace on <manifest>; leaving the file alone.");
            return;
        }

        int patched = 0;
        var filters = doc.SelectNodes("/manifest/application/activity/intent-filter");
        if (filters != null)
        {
            foreach (XmlNode filter in filters)
            {
                if (!HasChildNamed(filter, "action", MainAction, androidNs)) continue;      // only the entry point
                if (HasChildNamed(filter, "category", LauncherCategory, androidNs)) continue; // already fine

                XmlElement category = doc.CreateElement("category");
                category.SetAttribute("name", androidNs, LauncherCategory);
                filter.AppendChild(category);
                patched++;
            }
        }

        if (patched > 0)
        {
            doc.Save(manifestPath);
            Debug.Log($"[RestoreLauncherCategory] Re-added {LauncherCategory} to {patched} MAIN " +
                      $"intent-filter(s) in {manifestPath}. Build and Run can now launch the app.");
        }
        else
        {
            Debug.Log("[RestoreLauncherCategory] LAUNCHER category already present; nothing to do.");
        }
    }

    private static bool HasChildNamed(XmlNode parent, string childTag, string value, string androidNs)
    {
        var children = parent.SelectNodes(childTag);
        if (children == null) return false;
        foreach (XmlNode child in children)
        {
            if (child is XmlElement e && e.GetAttribute("name", androidNs) == value)
                return true;
        }
        return false;
    }
}
