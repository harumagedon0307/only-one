using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public class RemoveMissingScripts : EditorWindow
{
    [MenuItem("Tools/Find Missing Scripts (All Open Scenes)")]
    static void FindMissingInAll()
    {
        Debug.Log("===== Missing Scripts Search Started (All Open Scenes) =====");
        int count = 0;
        
        // 現在ロードされている全てのシーンを検索
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            Debug.Log($"Searching in scene: {scene.name}");
            count += FindMissingInScene(scene);
        }
        
        if (count > 0)
        {
            Debug.LogWarning($"===== Found {count} missing script(s) =====");
            EditorUtility.DisplayDialog("Missing Scripts Found", 
                $"{count}個の欠落したスクリプトが見つかりました。\nConsoleログでGameObjectの場所を確認してください。", "OK");
        }
        else
        {
            Debug.Log("===== No missing scripts found =====");
            EditorUtility.DisplayDialog("Success", "欠落したスクリプトは見つかりませんでした", "OK");
        }
    }

    [MenuItem("Tools/Find Missing Scripts (Current Scene)")]
    static void FindMissingInCurrentScene()
    {
        Debug.Log("===== Missing Scripts Search Started =====");
        var objs = GameObject.FindObjectsOfType<GameObject>(true); // includeInactive = true
        int count = 0;
        
        foreach (var obj in objs)
        {
            var components = obj.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    string path = GetGameObjectPath(obj);
                    Debug.LogError($"Missing Script Found: {path} (Component Index: {i})", obj);
                    count++;
                }
            }
        }
        
        if (count > 0)
        {
            Debug.LogWarning($"===== Found {count} missing script(s) =====");
            EditorUtility.DisplayDialog("Missing Scripts Found", 
                $"{count}個の欠落したスクリプトが見つかりました。\nConsoleログでGameObjectの場所を確認してください。", "OK");
        }
        else
        {
            Debug.Log("===== No missing scripts found =====");
            EditorUtility.DisplayDialog("Success", "欠落したスクリプトは見つかりませんでした", "OK");
        }
    }

    [MenuItem("Tools/Remove Missing Scripts (All Open Scenes)")]
    static void RemoveFromAll()
    {
        Debug.Log("===== Removing Missing Scripts Started (All Open Scenes) =====");
        int count = 0;
        
        // 現在ロードされている全てのシーンから削除
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            Debug.Log($"Removing from scene: {scene.name}");
            count += RemoveMissingInScene(scene);
        }
        
        Debug.Log($"===== Total removed: {count} missing script(s) =====");
        
        if (count > 0)
        {
            EditorUtility.DisplayDialog("Success", 
                $"削除完了: {count}個の欠落したスクリプトを削除しました\n\nシーンを保存してください (Ctrl+S)", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Info", "欠落したスクリプトは見つかりませんでした", "OK");
        }
    }

    [MenuItem("Tools/Remove Missing Scripts (Current Scene)")]
    static void RemoveFromCurrentScene()
    {
        Debug.Log("===== Removing Missing Scripts Started =====");
        var objs = GameObject.FindObjectsOfType<GameObject>(true); // includeInactive = true
        int count = 0;
        
        foreach (var obj in objs)
        {
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
            if (removed > 0)
            {
                string path = GetGameObjectPath(obj);
                Debug.Log($"Removed {removed} missing script(s) from: {path}");
                count += removed;
                EditorUtility.SetDirty(obj);
            }
        }
        
        Debug.Log($"===== Total removed: {count} missing script(s) =====");
        
        if (count > 0)
        {
            EditorUtility.DisplayDialog("Success", 
                $"削除完了: {count}個の欠落したスクリプトを削除しました\n\nシーンを保存してください (Ctrl+S)", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Info", "欠落したスクリプトは見つかりませんでした", "OK");
        }
    }
    
    // 特定のシーンから検索
    static int FindMissingInScene(Scene scene)
    {
        int count = 0;
        var rootObjects = scene.GetRootGameObjects();
        foreach (var obj in rootObjects)
        {
            count += FindMissingInGameObject(obj);
        }
        return count;
    }
    
    // 特定のシーンから削除
    static int RemoveMissingInScene(Scene scene)
    {
        int count = 0;
        var rootObjects = scene.GetRootGameObjects();
        foreach (var obj in rootObjects)
        {
            count += RemoveMissingInGameObject(obj);
        }
        return count;
    }
    
    // GameObjectとその子オブジェクトから欠落したスクリプトを検索
    static int FindMissingInGameObject(GameObject obj)
    {
        int count = 0;
        var components = obj.GetComponents<Component>();
        
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                string path = GetGameObjectPath(obj);
                Debug.LogError($"Missing Script Found: {path} (Component Index: {i}) [Scene: {obj.scene.name}]", obj);
                count++;
            }
        }
        
        foreach (Transform child in obj.transform)
        {
            count += FindMissingInGameObject(child.gameObject);
        }
        
        return count;
    }
    
    // GameObjectとその子オブジェクトから欠落したスクリプトを削除
    static int RemoveMissingInGameObject(GameObject obj)
    {
        int count = 0;
        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
        
        if (removed > 0)
        {
            string path = GetGameObjectPath(obj);
            Debug.Log($"Removed {removed} missing script(s) from: {path} [Scene: {obj.scene.name}]");
            count += removed;
            EditorUtility.SetDirty(obj);
        }
        
        foreach (Transform child in obj.transform)
        {
            count += RemoveMissingInGameObject(child.gameObject);
        }
        
        return count;
    }
    
    // GameObjectの階層パスを取得
    static string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform current = obj.transform.parent;
        
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        
        return path;
    }
}
