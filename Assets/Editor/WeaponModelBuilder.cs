using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Инструмент сборки префабов оружия из FBX-моделей.
// Нормализует габариты/ориентацию, назначает материалы и собирает каталог
// WeaponModelCatalog. Запуск: меню Tools/Оружие/Собрать модели оружия.
public static class WeaponModelBuilder
{
    private const string ModelsRoot = "Assets/Models/Weapons";
    private const string PrefabsRoot = "Assets/Prefabs/Weapons";
    private const string CatalogPath = "Assets/Resources/WeaponModelCatalog.asset";

    private const float DefaultPivotRear = 0.4f;

    private class ModelDef
    {
        public WeaponType type;
        public string fbxAssetPath;
        public float targetLength;
        public float pivotRear = DefaultPivotRear;
        public bool grenade;
        public bool muzzleNegX;
        public bool muzzleNegY;
        public bool disableScopeRenderers;
        // Некоторые FBX содержат несколько вариантов оружия рядом друг с другом.
        // Здесь — локальный X выбранного варианта: остальные рендереры отключаются.
        public float keepVariantX = float.NaN;

        public ModelDef(WeaponType type, string folder, string file, float targetLength)
        {
            this.type = type;
            fbxAssetPath = ModelsRoot + "/" + folder + "/" + file;
            this.targetLength = targetLength;
        }
    }

    private static ModelDef Def(WeaponType type, string folder, string file, float length)
    {
        return new ModelDef(type, folder, file, length);
    }

    [MenuItem("Tools/Оружие/Собрать модели оружия")]
    public static void BuildAll()
    {
        List<ModelDef> defs = new List<ModelDef>
        {
            Def(WeaponType.Mosin, "Mosin", "Mosin.fbx", 1.15f),
            Def(WeaponType.MosinSniper, "Mosin", "Mosin.fbx", 1.15f),
            Def(WeaponType.PPSH, "PPSH", "PPSH.fbx", 0.85f),
            Def(WeaponType.MP40, "MP40", "MP40.fbx", 0.72f),
            Def(WeaponType.MP38, "MP40", "MP40.fbx", 0.72f),
            Def(WeaponType.Sturmgewehr, "Sturmgewehr", "Sturmgewehr.fbx", 0.95f),
            Def(WeaponType.Panzerfaust, "Panzerfaust", "Panzerfaust.fbx", 1.2f),
            Def(WeaponType.WaltherP38, "WaltherP38", "WaltherP38.fbx", 0.21f),
            Def(WeaponType.LugerP08, "LugerP08", "LugerP08.fbx", 0.22f),
            Def(WeaponType.MauserC96, "MauserC96", "MauserC96.fbx", 0.30f),
            Def(WeaponType.WaltherPP, "WaltherPP", "WaltherPP.fbx", 0.17f),
            Def(WeaponType.MP28, "MP28", "MP28.fbx", 0.82f),
            Def(WeaponType.Kar98k, "Kar98k", "Kar98k.fbx", 1.12f),
            Def(WeaponType.Gewehr43, "Gewehr43", "Gewehr43.fbx", 1.10f),
            Def(WeaponType.FG42, "FG42", "FG42.fbx", 1.0f),
            Def(WeaponType.MG34, "MG34", "MG34.fbx", 1.15f),
            Def(WeaponType.MG42, "MG42", "MG42.fbx", 1.15f),
            Def(WeaponType.Panzerschreck, "Panzerschreck", "Panzerschreck.fbx", 1.35f),
            Def(WeaponType.PzB39, "PzB39", "PzB39.fbx", 1.35f),
            Def(WeaponType.Stielhandgranate24, "Stielhandgranate24", "Stielhandgranate24.fbx", 0.36f),
            Def(WeaponType.Eierhandgranate39, "Eierhandgranate39", "Eierhandgranate39.fbx", 0.08f),
            Def(WeaponType.TT33, "TT33", "TT33.fbx", 0.20f),
            Def(WeaponType.Nagant1895, "Nagant1895", "Nagant1895.fbx", 0.31f),
            Def(WeaponType.PPD40, "PPD40", "PPD40.fbx", 0.82f),
            Def(WeaponType.SVT40, "SVT40", "SVT40.fbx", 1.15f),
            Def(WeaponType.AVT40, "AVT40", "AVT40.fbx", 1.15f),
            Def(WeaponType.AVS36, "AVS36", "AVS36.fbx", 1.15f),
            Def(WeaponType.DP27, "DP27", "DP27.fbx", 1.15f),
            Def(WeaponType.Maxim1910, "Maxim1910", "Maxim1910.fbx", 1.25f),
            Def(WeaponType.DShK, "DShK", "DShK.fbx", 1.25f),
            Def(WeaponType.DT29, "DT29", "DT29.fbx", 1.05f),
            Def(WeaponType.PTRD41, "PTRD41", "PTRD41.fbx", 1.45f),
            Def(WeaponType.PTRS41, "PTRS41", "PTRS41.fbx", 1.45f),
            Def(WeaponType.F1, "F1", "F1.fbx", 0.11f),
            Def(WeaponType.RGD33, "RGD33", "RGD33.fbx", 0.11f),
            Def(WeaponType.RPG43, "RPG43", "RPG43.fbx", 0.10f),
            Def(WeaponType.Knife, "Knife", "Knife.fbx", 0.55f),
        };

        // Персональные настройки.
        foreach (ModelDef def in defs)
        {
            switch (def.type)
            {
                case WeaponType.Mosin:
                    // В Mosin.fbx три винтовки: M38, Мосин 1891/30 и снайперская.
                    def.disableScopeRenderers = true;
                    def.keepVariantX = -0.048f;
                    break;
                case WeaponType.MosinSniper:
                    // Снайперский вариант (с прицелом PE) стоит правее остальных.
                    def.keepVariantX = 0.214f;
                    break;
                case WeaponType.Kar98k:
                    // В Kar98k.fbx два варианта: с оптикой и со штыком.
                    // Оставляем первый, оптику отключаем — нужна обычная винтовка.
                    def.keepVariantX = -0.140f;
                    def.disableScopeRenderers = true;
                    break;
                case WeaponType.MauserC96:
                case WeaponType.WaltherPP:
                    def.pivotRear = 0.12f;
                    break;
                case WeaponType.WaltherP38:
                case WeaponType.LugerP08:
                case WeaponType.TT33:
                case WeaponType.Nagant1895:
                    def.pivotRear = 0.12f;
                    break;
                case WeaponType.MP28:
                    def.muzzleNegX = true;
                    break;
                case WeaponType.Knife:
                    def.pivotRear = 0.12f;
                    break;
                case WeaponType.Panzerfaust:
                case WeaponType.Panzerschreck:
                    def.pivotRear = 0.3f;
                    break;
                case WeaponType.F1:
                case WeaponType.RGD33:
                case WeaponType.RPG43:
                case WeaponType.Stielhandgranate24:
                case WeaponType.Eierhandgranate39:
                    def.grenade = true;
                    def.pivotRear = 0.5f;
                    break;
                case WeaponType.MG34:
                case WeaponType.MG42:
                case WeaponType.DP27:
                case WeaponType.Maxim1910:
                case WeaponType.DShK:
                case WeaponType.DT29:
                case WeaponType.PTRD41:
                case WeaponType.PTRS41:
                case WeaponType.PzB39:
                    def.pivotRear = 0.42f;
                    break;
            }
        }

        EnsureFolder(PrefabsRoot);
        EnsureFolder("Assets/Resources");

        Scene buildScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        List<WeaponModelCatalog.Entry> catalogEntries = new List<WeaponModelCatalog.Entry>();
        int built = 0;
        try
        {
            foreach (ModelDef def in defs)
            {
                if (!File.Exists(def.fbxAssetPath))
                {
                    Debug.LogWarning("[WeaponModelBuilder] Нет FBX: " + def.fbxAssetPath + " — пропускаю " + def.type);
                    continue;
                }

                GameObject prefab = BuildOne(def, buildScene);
                if (prefab == null)
                {
                    continue;
                }

                WeaponModelCatalog.Entry entry = new WeaponModelCatalog.Entry();
                entry.type = def.type;
                entry.prefab = prefab;
                catalogEntries.Add(entry);
                built++;
            }

            SaveCatalog(catalogEntries);
        }
        finally
        {
            EditorSceneManager.CloseScene(buildScene, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log("[WeaponModelBuilder] Готово: собрано префабов " + built + " из " + defs.Count + ".");
    }

    private static GameObject BuildOne(ModelDef def, Scene buildScene)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(def.fbxAssetPath);
        if (asset == null)
        {
            Debug.LogWarning("[WeaponModelBuilder] Не удалось загрузить " + def.fbxAssetPath);
            return null;
        }

        string folder = Path.GetDirectoryName(def.fbxAssetPath).Replace('\\', '/');
        PrepareTextures(folder);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, buildScene);
        if (instance == null)
        {
            return null;
        }

        if (def.disableScopeRenderers)
        {
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.ToLowerInvariant().Contains("scope"))
                {
                    r.enabled = false;
                }
            }
        }

        // В FBX вроде Mosin/Kar98k лежит несколько вариантов оружия подряд.
        // Оставляем только выбранный вариант, остальные рендереры выключаем —
        // иначе в руках видно сразу две-три винтовки, а общая длина делится между ними.
        if (!float.IsNaN(def.keepVariantX))
        {
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (r.enabled && Mathf.Abs(r.bounds.center.x - def.keepVariantX) > 0.1f)
                {
                    r.enabled = false;
                }
            }
        }

        Bounds bounds = ComputeBounds(instance);
        Vector3 size = bounds.size;

        bool xLong = size.x > size.y && size.x > size.z;
        bool yLong = size.y > size.x && size.y > size.z;
        Quaternion rotation = Quaternion.identity;
        if (!def.grenade)
        {
            if (xLong)
            {
                rotation = Quaternion.Euler(0f, def.muzzleNegX ? 90f : -90f, 0f);
            }
            else if (yLong)
            {
                rotation = Quaternion.Euler(def.muzzleNegY ? -90f : 90f, 0f, 0f);
            }
        }

        Bounds rotatedBounds = RotateBounds(bounds, rotation);
        float length = def.grenade
            ? Mathf.Max(rotatedBounds.size.x, Mathf.Max(rotatedBounds.size.y, rotatedBounds.size.z))
            : rotatedBounds.size.z;
        float scale = def.targetLength / Mathf.Max(length, 0.0001f);

        Vector3 desiredCenter = def.grenade
            ? new Vector3(0f, 0f, 0.05f)
            : new Vector3(0f, 0f, (0.5f - def.pivotRear) * def.targetLength);

        GameObject root = new GameObject(def.type.ToString());
        SceneManager.MoveGameObjectToScene(root, buildScene);

        GameObject model = new GameObject("Model");
        model.transform.SetParent(root.transform, false);
        instance.transform.SetParent(model.transform, false);

        model.transform.localRotation = rotation;
        model.transform.localScale = Vector3.one * scale;
        model.transform.localPosition = desiredCenter - (rotation * (bounds.center * scale));

        AssignMaterials(def, folder, instance);

        GameObject muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(root.transform, false);
        float muzzleZ = def.grenade ? 0f : desiredCenter.z + def.targetLength * 0.5f + 0.015f;
        muzzle.transform.localPosition = new Vector3(0f, 0f, muzzleZ);

        string prefabPath = PrefabsRoot + "/" + def.type + ".prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static Bounds ComputeBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        bool has = false;
        Bounds bounds = new Bounds();
        foreach (Renderer r in renderers)
        {
            if (!r.enabled)
            {
                continue;
            }

            if (!has)
            {
                bounds = r.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return bounds;
    }

    private static Bounds RotateBounds(Bounds source, Quaternion rotation)
    {
        Vector3 center = source.center;
        Vector3 extents = source.extents;
        bool has = false;
        Bounds result = new Bounds();
        for (int i = 0; i < 8; i++)
        {
            Vector3 point = center + new Vector3(
                (i & 1) == 0 ? -extents.x : extents.x,
                (i & 2) == 0 ? -extents.y : extents.y,
                (i & 4) == 0 ? -extents.z : extents.z);
            point = rotation * point;
            if (!has)
            {
                result = new Bounds(point, Vector3.zero);
                has = true;
            }
            else
            {
                result.Encapsulate(point);
            }
        }

        return result;
    }

    private static void AssignMaterials(ModelDef def, string folder, GameObject instance)
    {
        Dictionary<string, Material> created = new Dictionary<string, Material>();
        string materialsFolder = folder + "/Materials";
        EnsureFolder(materialsFolder);

        Texture2D folderAlbedo = FindTexture(folder, false);
        Texture2D folderNormal = FindTexture(folder, true);

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material source = renderer.sharedMaterial;

            Texture2D albedo = source != null ? source.mainTexture as Texture2D : null;
            if (albedo == null)
            {
                albedo = folderAlbedo;
            }

            Texture2D normal = source != null ? source.GetTexture("_BumpMap") as Texture2D : null;
            if (normal == null)
            {
                normal = folderNormal;
            }

            string key;
            Material material;
            if (albedo != null)
            {
                string baseName = source != null && !string.IsNullOrEmpty(source.name) ? source.name : albedo.name;
                key = "tex_" + baseName;
                if (!created.TryGetValue(key, out material))
                {
                    material = CreateMaterial(def, materialsFolder + "/" + Sanitize(baseName) + ".mat",
                        albedo, normal, Color.white, 0.15f, 0.35f);
                    created[key] = material;
                }
            }
            else
            {
                bool wood = IsWoodRenderer(renderer.name) && !def.grenade && def.type != WeaponType.Knife;
                string style = wood ? "Wood" : "Metal";
                key = "flat_" + style;
                if (!created.TryGetValue(key, out material))
                {
                    Color color = wood ? WoodColor(def.type) : MetalColor(def.type);
                    material = CreateMaterial(def, materialsFolder + "/" + style + ".mat",
                        null, null, color, wood ? 0f : 0.35f, wood ? 0.2f : 0.4f);
                    created[key] = material;
                }
            }

            renderer.sharedMaterial = material;
        }
    }

    private static Material CreateMaterial(
        ModelDef def, string assetPath, Texture2D albedo, Texture2D normal, Color color,
        float metallic, float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, assetPath);
        }
        else
        {
            material.shader = Shader.Find("Standard");
        }

        material.mainTexture = albedo;
        material.color = albedo != null ? Color.white : color;
        if (normal != null)
        {
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }
        else
        {
            material.SetTexture("_BumpMap", null);
            material.DisableKeyword("_NORMALMAP");
        }

        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void PrepareTextures(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        string[] files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
        foreach (string file in files)
        {
            if (!IsTextureFile(file))
            {
                continue;
            }

            if (!IsNormalName(Path.GetFileName(file)))
            {
                continue;
            }

            string assetPath = file.Replace('\\', '/');
            int index = assetPath.IndexOf("Assets/", System.StringComparison.Ordinal);
            if (index < 0)
            {
                continue;
            }

            assetPath = assetPath.Substring(index);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }
    }

    private static Texture2D FindTexture(string folder, bool normal)
    {
        if (!Directory.Exists(folder))
        {
            return null;
        }

        string[] files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
        string best = null;
        int bestScore = -1;
        foreach (string file in files)
        {
            if (!IsTextureFile(file))
            {
                continue;
            }

            string name = Path.GetFileName(file);
            bool isNormal = IsNormalName(name);
            if (isNormal != normal)
            {
                continue;
            }

            int score = 0;
            string lower = name.ToLowerInvariant();
            if (lower.Contains("albedo") || lower.Contains("basecolor") || lower.Contains("base_color") || lower.Contains("diffuse"))
            {
                score += 10;
            }

            if (lower.Contains("main"))
            {
                score += 5;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = file;
            }
        }

        if (best == null)
        {
            return null;
        }

        string assetPath = best.Replace('\\', '/');
        int index = assetPath.IndexOf("Assets/", System.StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        assetPath = assetPath.Substring(index);
        if (normal)
        {
            PrepareTextures(folder);
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    private static bool IsTextureFile(string file)
    {
        string ext = Path.GetExtension(file).ToLowerInvariant();
        return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga";
    }

    private static bool IsNormalName(string name)
    {
        string lower = name.ToLowerInvariant();
        return lower.Contains("normal") || lower.Contains("_n.") || lower.Contains("_n_") || lower.EndsWith("_n");
    }

    private static bool IsWoodRenderer(string name)
    {
        string lower = name.ToLowerInvariant();
        return lower.Contains("wood") || lower.Contains("furnit") || lower.Contains("stock") ||
               lower.Contains("bakelite") || lower.Contains("grip") || lower.Contains("handle") ||
               lower.Contains("butt") || lower.Contains("maderas");
    }

    private static bool IsGerman(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Sturmgewehr:
            case WeaponType.MP40:
            case WeaponType.MP38:
            case WeaponType.MP28:
            case WeaponType.WaltherP38:
            case WeaponType.LugerP08:
            case WeaponType.MauserC96:
            case WeaponType.WaltherPP:
            case WeaponType.Kar98k:
            case WeaponType.Gewehr41:
            case WeaponType.Gewehr43:
            case WeaponType.FG42:
            case WeaponType.MG34:
            case WeaponType.MG42:
            case WeaponType.MG13:
            case WeaponType.Panzerschreck:
            case WeaponType.PzB39:
            case WeaponType.Stielhandgranate24:
            case WeaponType.Eierhandgranate39:
            case WeaponType.Panzerfaust:
                return true;
            default:
                return false;
        }
    }

    private static Color WoodColor(WeaponType type)
    {
        return IsGerman(type)
            ? new Color(0.36f, 0.25f, 0.15f)
            : new Color(0.42f, 0.29f, 0.16f);
    }

    private static Color MetalColor(WeaponType type)
    {
        if (type == WeaponType.Knife)
        {
            return new Color(0.56f, 0.57f, 0.55f);
        }

        if (type == WeaponType.F1 || type == WeaponType.RGD33 || type == WeaponType.RPG43 ||
            type == WeaponType.Stielhandgranate24 || type == WeaponType.Eierhandgranate39)
        {
            return new Color(0.25f, 0.30f, 0.21f);
        }

        return IsGerman(type)
            ? new Color(0.19f, 0.20f, 0.19f)
            : new Color(0.22f, 0.23f, 0.20f);
    }

    private static string Sanitize(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "Material";
        }

        char[] chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }

    private static void SaveCatalog(List<WeaponModelCatalog.Entry> entries)
    {
        WeaponModelCatalog catalog = AssetDatabase.LoadAssetAtPath<WeaponModelCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<WeaponModelCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.entries = entries.ToArray();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }
}
