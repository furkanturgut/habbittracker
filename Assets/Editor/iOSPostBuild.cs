#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class iOSPostBuild
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;

        string projPath = PBXProject.GetPBXProjectPath(path);
        var proj = new PBXProject();
        proj.ReadFromFile(projPath);

        string frameworkTarget = proj.GetUnityFrameworkTargetGuid();
        proj.AddFrameworkToProject(frameworkTarget, "UniformTypeIdentifiers.framework", true);

        proj.WriteToFile(projPath);
    }
}
#endif