#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

/// <summary>
/// Outil de développement : permet de piloter l'éditeur depuis un fichier de commandes
/// (Temp/claude_cmd.txt) et écrit les logs dans Temp/claude_log.txt.
/// Commandes : refresh | play | stop | pause | exec Type.Methode | timescale X | clearlog
/// Sans effet sur le jeu final (éditeur uniquement).
/// </summary>
[InitializeOnLoad]
public static class ClaudeBridge
{
    private static readonly string CmdPath = Path.Combine("Temp", "claude_cmd.txt");
    private static readonly string LogPath = Path.Combine("Temp", "claude_log.txt");
    private static double nextPoll;

    static ClaudeBridge()
    {
        PlayerSettings.runInBackground = true;
        EditorApplication.update += Poll;
        Application.logMessageReceivedThreaded += OnLog;
        CompilationPipeline.assemblyCompilationFinished += OnCompiled;
        EditorApplication.playModeStateChanged += s => Write($"[PLAYMODE] {s}");
        Write($"[BRIDGE] loaded {DateTime.Now:HH:mm:ss}");
    }

    private static void OnCompiled(string asm, CompilerMessage[] msgs)
    {
        foreach (var m in msgs)
            if (m.type == CompilerMessageType.Error)
                Write($"[COMPILE ERROR] {m.file}({m.line}): {m.message}");
    }

    private static void OnLog(string msg, string stack, LogType type)
    {
        string line = $"[{type}] {msg}";
        if (type == LogType.Exception || type == LogType.Error)
        {
            string[] st = stack.Split('\n');
            for (int i = 0; i < Math.Min(4, st.Length); i++) line += "\n    " + st[i];
        }
        Write(line);
    }

    private static readonly object fileLock = new object();
    private static void Write(string line)
    {
        try { lock (fileLock) File.AppendAllText(LogPath, line + "\n"); } catch { }
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.3;
        if (!File.Exists(CmdPath)) return;

        string[] cmds;
        try { cmds = File.ReadAllLines(CmdPath); File.Delete(CmdPath); }
        catch { return; }

        foreach (var raw in cmds)
        {
            string cmd = raw.Trim();
            if (cmd.Length == 0) continue;
            Write($"[CMD] {cmd}");
            try { Execute(cmd); }
            catch (Exception e) { Write($"[CMD FAILED] {e}"); }
        }
    }

    private static void Execute(string cmd)
    {
        string[] parts = cmd.Split(new[] { ' ' }, 2);
        switch (parts[0])
        {
            case "refresh": AssetDatabase.Refresh(); break;
            case "play": EditorApplication.isPlaying = true; break;
            case "stop": EditorApplication.isPlaying = false; break;
            case "pause": EditorApplication.isPaused = !EditorApplication.isPaused; break;
            case "clearlog": File.WriteAllText(LogPath, ""); break;
            case "timescale": Time.timeScale = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture); break;
            case "savescene": UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes(); break;
            case "exec":
                int dot = parts[1].LastIndexOf('.');
                string typeName = parts[1].Substring(0, dot), method = parts[1].Substring(dot + 1);
                Type t = null;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = a.GetType(typeName);
                    if (t != null) break;
                }
                if (t == null) { Write($"[CMD] type introuvable {typeName}"); return; }
                var mi = t.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                object r = mi.Invoke(null, null);
                Write($"[CMD RESULT] {r}");
                break;
            default: Write($"[CMD] inconnue: {cmd}"); break;
        }
    }
}
#endif
