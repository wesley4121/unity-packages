using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;

public class AtlasExtractor : EditorWindow
{
    private Texture2D atlasTexture;
    private TextAsset dataFile;
    private string selectedDirectory = "Assets";
    private Dictionary<string, Sprite> spriteDict = new Dictionary<string, Sprite>();

    // 中繼資料結構，用於統一處理來自不同格式的 Sprite 資訊
    private class SpriteFrameData
    {
        public string Name;
        public int X, Y, W, H;
        public bool Rotated;
    }

    [MenuItem("Tools/AtlasExtractor")]
    public static void ShowWindow()
    {
        GetWindow<AtlasExtractor>("AtlasExtractor");
    }

    void OnGUI()
    {
        atlasTexture = (Texture2D)EditorGUILayout.ObjectField("Atlas Texture", atlasTexture, typeof(Texture2D), false);
        dataFile = (TextAsset)EditorGUILayout.ObjectField("Data File (JSON/XML)", dataFile, typeof(TextAsset), false);

        if (GUILayout.Button("Create Sprites") && atlasTexture != null && dataFile != null)
        {
            CreateSprites();
        }

        if (GUILayout.Button("Export PNGs") && atlasTexture != null && dataFile != null)
        {
            ExportPNGs();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Batch Process by Directory", EditorStyles.boldLabel);
        EditorGUILayout.TextField("Directory", selectedDirectory);
        if (GUILayout.Button("Select Directory"))
        {
            string path = EditorUtility.OpenFolderPanel("Select Directory", selectedDirectory, "");
            if (!string.IsNullOrEmpty(path))
            {
                // Convert absolute path to a relative path starting with "Assets"
                if (path.StartsWith(Application.dataPath))
                {
                    selectedDirectory = "Assets" + path.Substring(Application.dataPath.Length);
                }
                else
                {
                    Debug.LogWarning("Please select a folder within the project's Assets directory.");
                }
            }
        }

        if (GUILayout.Button("Export All PNGs in Directory"))
        {
            ProcessDirectory();
        }

        if (spriteDict.Count > 0)
        {
            EditorGUILayout.LabelField($"Created {spriteDict.Count} Sprites");
            // 為了避免 UI 過於冗長，可以考慮只顯示部分或不顯示
            // foreach (var kv in spriteDict)
            // {
            //     EditorGUILayout.LabelField(kv.Key);
            // }
        }
    }

    private void ProcessDirectory()
    {
        if (string.IsNullOrEmpty(selectedDirectory) || !Directory.Exists(selectedDirectory))
        {
            Debug.LogError("Please select a valid directory first.");
            return;
        }

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { selectedDirectory });
        int processedCount = 0;

        foreach (string guid in textureGuids)
        {
            string texturePath = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

            if (texture == null) continue;

            string dataPathWithoutExt = Path.Combine(Path.GetDirectoryName(texturePath), Path.GetFileNameWithoutExtension(texturePath));

            string[] possibleExtensions = { ".json", ".fnt", ".xml", ".txt" };
            TextAsset data = null;

            foreach (var ext in possibleExtensions)
            {
                data = AssetDatabase.LoadAssetAtPath<TextAsset>(dataPathWithoutExt + ext);
                if (data != null) break;
            }

            if (texture != null && data != null)
            {
                Debug.Log($"Processing: {texture.name}");
                ExportPNGsFor(texture, data);
                processedCount++;
            }
        }

        if (processedCount > 0)
        {
            EditorUtility.DisplayDialog("Batch Process Complete", $"Successfully processed and exported {processedCount} atlases.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Batch Process Complete", "No matching texture and data file pairs found in the selected directory.", "OK");
        }
    }

    // 根據檔案內容決定要用哪種解析器
    private List<SpriteFrameData> ParseDataFile()
    {
        string text = dataFile.text.Trim();
        if (text.StartsWith("<"))
        {
            return ParseXml(text);
        }
        else
        {
            return ParseJson(text);
        }
    }

    private List<SpriteFrameData> ParseDataFileFor(TextAsset data)
    {
        string text = data.text.Trim();
        if (text.StartsWith("<"))
        {
            return ParseXml(text);
        }
        else
        {
            return ParseJson(text);
        }
    }

    // 解析 JSON 格式 (TexturePacker)
    private List<SpriteFrameData> ParseJson(string jsonText)
    {
        var framesData = new List<SpriteFrameData>();
        try
        {
            var json = JObject.Parse(jsonText);
            var frames = json["frames"] as JObject;
            if (frames == null)
            {
                Debug.LogError("Could not find 'frames' object in the JSON file.");
                return framesData;
            }

            foreach (var framePair in frames)
            {
                framesData.Add(new SpriteFrameData
                {
                    Name = framePair.Key,
                    X = framePair.Value["frame"]["x"].Value<int>(),
                    Y = framePair.Value["frame"]["y"].Value<int>(),
                    W = framePair.Value["frame"]["w"].Value<int>(),
                    H = framePair.Value["frame"]["h"].Value<int>(),
                    Rotated = framePair.Value["rotated"].Value<bool>()
                });
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error parsing JSON file '{dataFile.name}'. Details: {e.Message}");
        }
        return framesData;
    }

    // 解析 XML 格式 (.fnt)
    private List<SpriteFrameData> ParseXml(string xmlText)
    {
        var framesData = new List<SpriteFrameData>();
        try
        {
            var doc = XDocument.Parse(xmlText);
            var chars = doc.Descendants("char");
            foreach (var c in chars)
            {
                int charId = (int)c.Attribute("id");
                framesData.Add(new SpriteFrameData
                {
                    Name = ((char)charId).ToString(),
                    X = (int)c.Attribute("x"),
                    Y = (int)c.Attribute("y"),
                    W = (int)c.Attribute("width"),
                    H = (int)c.Attribute("height"),
                    Rotated = false // .fnt 格式通常不包含旋轉資訊
                });
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error parsing XML file '{dataFile.name}'. Details: {e.Message}");
        }
        return framesData;
    }


    private void CreateSprites()
    {
        spriteDict.Clear();
        var frames = ParseDataFile();
        if (frames == null || frames.Count == 0) return;

        foreach (var frame in frames)
        {
            int unityY = atlasTexture.height - frame.Y - (frame.Rotated ? frame.W : frame.H);

            Rect rect = frame.Rotated
                ? new Rect(frame.X, unityY, frame.H, frame.W)
                : new Rect(frame.X, unityY, frame.W, frame.H);

            Vector2 pivot = new Vector2(0.5f, 0.5f);

            Sprite sprite = Sprite.Create(
                atlasTexture,
                rect,
                pivot,
                100,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero,
                frame.Rotated
            );

            sprite.name = frame.Name;
            spriteDict[frame.Name] = sprite;
        }

        Debug.Log($"Created {spriteDict.Count} Sprites");
    }

    private void ExportPNGs()
    {
        if (atlasTexture == null || dataFile == null)
        {
            Debug.LogError("Please specify the Atlas and Data file first");
            return;
        }
        string exportDir = ExportPNGsFor(atlasTexture, dataFile);
        if (!string.IsNullOrEmpty(exportDir))
        {
            EditorUtility.DisplayDialog("Export PNGs", $"Exported to: {exportDir}", "OK");
        }
    }

    private string ExportPNGsFor(Texture2D texture, TextAsset data)
    {
        var frames = ParseDataFileFor(data);
        if (frames == null || frames.Count == 0) return null;

        string text = data.text.Trim();
        string atlasPath = AssetDatabase.GetAssetPath(texture);
        string atlasDir = Path.GetDirectoryName(atlasPath);
        string atlasName = Path.GetFileNameWithoutExtension(atlasPath);
        string exportDir = Path.Combine(atlasDir, atlasName);

        if (!Directory.Exists(exportDir))
            Directory.CreateDirectory(exportDir);

        foreach (var frame in frames)
        {
            int unityY = texture.height - frame.Y - (frame.Rotated ? frame.W : frame.H);

            Color[] pixels;
            Texture2D tex;

            if (frame.Rotated)
            {
                pixels = texture.GetPixels(frame.X, unityY, frame.H, frame.W);
                pixels = RotatePixelsCW(pixels, frame.H, frame.W);
                tex = new Texture2D(frame.W, frame.H, TextureFormat.RGBA32, false);
            }
            else
            {
                pixels = texture.GetPixels(frame.X, unityY, frame.W, frame.H);
                tex = new Texture2D(frame.W, frame.H, TextureFormat.RGBA32, false);
            }

            tex.SetPixels(pixels);
            tex.Apply();

            // 對於 XML 格式，使用 char ID 作為檔名以避免特殊字元問題
            string fileName = text.StartsWith("<")
                ? ((int)frame.Name[0]).ToString() + ".png"
                : Path.GetFileNameWithoutExtension(frame.Name) + ".png";

            string filePath = Path.Combine(exportDir, fileName);
            File.WriteAllBytes(filePath, tex.EncodeToPNG());
            DestroyImmediate(tex);
        }

        AssetDatabase.Refresh();
        return exportDir;
    }

    // Rotate pixel array 90 degrees clockwise
    private Color[] RotatePixelsCW(Color[] pixels, int width, int height)
    {
        Color[] rotated = new Color[pixels.Length];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                rotated[x * height + (height - y - 1)] = pixels[y * width + x];
        return rotated;
    }
}