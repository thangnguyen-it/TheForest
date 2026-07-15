using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TheForest.Building;
using TheForest.Items;
using TheForest.Multiplayer;
using TheForest.World;

namespace TheForest.EditorTools
{
    public static class WorldSurvivalSceneSetup
    {
        private const string RootName = "_SurvivalCamp_Runtime";
        private const string ItemPath = "SotFData/Items";

        [MenuItem("TheForest/Setup/Build Survival Camp Placeholders")]
        public static void BuildSurvivalCampPlaceholders()
        {
            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
            }

            Transform player = GameObject.FindWithTag("Player")?.transform;
            Vector3 origin = player != null ? player.position + player.forward * 5f + player.right * 2f : new Vector3(0f, 0f, 0f);
            root.transform.position = origin;

            CreateCampfire(root.transform, new Vector3(0f, 0f, 0f));
            CreateRainCatcher(root.transform, new Vector3(2.2f, 0f, 0.2f));
            CreateCookingPot(root.transform, new Vector3(0.8f, 0.35f, 0f));
            CreateDryingRack(root.transform, new Vector3(-2.2f, 0.6f, 0f));
            CreateStickStorage(root.transform, new Vector3(-0.2f, 0.35f, 2.2f));

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            Debug.Log("[WorldSurvivalSceneSetup] Survival camp placeholders are ready in the active scene.");
        }

        private static void CreateCampfire(Transform parent, Vector3 localPosition)
        {
            GameObject fire = GetOrCreatePrimitive(parent, "Runtime_Campfire", PrimitiveType.Cylinder, localPosition, new Vector3(1f, 0.15f, 1f), SurvivalMat("Charcoal", new Color(0.08f, 0.07f, 0.06f)));
            EnsureNetworked(fire);
            FireSource source = Ensure<FireSource>(fire);
            CampfireController controller = Ensure<CampfireController>(fire);

            GameObject stick1 = GetOrCreatePrimitive(fire.transform, "StickStageVisual1", PrimitiveType.Cube, new Vector3(-0.18f, 0.25f, 0f), new Vector3(0.55f, 0.06f, 0.08f), SurvivalMat("StickBrown", new Color(0.32f, 0.18f, 0.08f)));
            stick1.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
            stick1.SetActive(false);

            GameObject stick2 = GetOrCreatePrimitive(fire.transform, "StickStageVisual2", PrimitiveType.Cube, new Vector3(0.18f, 0.3f, 0f), new Vector3(0.55f, 0.06f, 0.08f), SurvivalMat("StickBrown", new Color(0.32f, 0.18f, 0.08f)));
            stick2.transform.localRotation = Quaternion.Euler(0f, -25f, 0f);
            stick2.SetActive(false);

            GameObject flame = GetOrCreatePrimitive(fire.transform, "FlameVisual", PrimitiveType.Sphere, new Vector3(0f, 0.55f, 0f), new Vector3(0.35f, 0.65f, 0.35f), SurvivalMat("FlameOrange", new Color(1f, 0.36f, 0.04f)));
            flame.SetActive(false);

            GameObject ring = GetOrCreatePrimitive(fire.transform, "ReinforcedRingVisual", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), new Vector3(1.35f, 0.08f, 1.35f), SurvivalMat("StoneGray", new Color(0.28f, 0.28f, 0.26f)));
            ring.SetActive(false);

            SerializedObject so = new SerializedObject(controller);
            SetObject(so, "stickItem", Item("stick"));
            SetObject(so, "largeRockItem", Item("rock"));
            SetObject(so, "firewoodItem", Item("log_plank"));
            SetObject(so, "stickStageVisual1", stick1);
            SetObject(so, "stickStageVisual2", stick2);
            SetObject(so, "litFlameVisual", flame);
            SetObject(so, "reinforcedRingVisual", ring);
            so.ApplyModifiedPropertiesWithoutUndo();

            source.SetBurning(false);
            EditorUtility.SetDirty(fire);
        }

        private static void CreateRainCatcher(Transform parent, Vector3 localPosition)
        {
            GameObject catcher = GetOrCreatePrimitive(parent, "Runtime_RainCatcher", PrimitiveType.Cylinder, localPosition, new Vector3(1.1f, 0.22f, 1.1f), SurvivalMat("ShellDark", new Color(0.16f, 0.13f, 0.1f)));
            EnsureNetworked(catcher);
            RainCatcher controller = Ensure<RainCatcher>(catcher);

            GameObject water = GetOrCreatePrimitive(catcher.transform, "WaterVisual", PrimitiveType.Cylinder, new Vector3(0f, 0.22f, 0f), new Vector3(0.85f, 0.04f, 0.85f), SurvivalMat("WaterBlue", new Color(0.04f, 0.38f, 0.72f, 0.75f)));
            SerializedObject so = new SerializedObject(controller);
            SetObject(so, "cleanWaterItem", Item("clean_water"));
            SetObject(so, "waterVisual", water);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catcher);
        }

        private static void CreateCookingPot(Transform parent, Vector3 localPosition)
        {
            GameObject pot = GetOrCreatePrimitive(parent, "Runtime_CookingPot", PrimitiveType.Cylinder, localPosition, new Vector3(0.55f, 0.35f, 0.55f), SurvivalMat("IronBlack", new Color(0.04f, 0.04f, 0.045f)));
            EnsureNetworked(pot);
            CookingPotController controller = Ensure<CookingPotController>(pot);

            GameObject water = GetOrCreatePrimitive(pot.transform, "WaterVisual", PrimitiveType.Cylinder, new Vector3(0f, 0.42f, 0f), new Vector3(0.42f, 0.04f, 0.42f), SurvivalMat("WaterBlue", new Color(0.04f, 0.38f, 0.72f, 0.75f)));
            GameObject boil = GetOrCreatePrimitive(pot.transform, "BoilingVisual", PrimitiveType.Sphere, new Vector3(0f, 0.55f, 0f), new Vector3(0.18f, 0.18f, 0.18f), SurvivalMat("SteamWhite", new Color(0.8f, 0.85f, 0.9f, 0.55f)));
            water.SetActive(false);
            boil.SetActive(false);

            SerializedObject so = new SerializedObject(controller);
            SetObject(so, "waterVisual", water);
            SetObject(so, "boilingVisual", boil);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pot);
        }

        private static void CreateDryingRack(Transform parent, Vector3 localPosition)
        {
            GameObject rack = GetOrCreatePrimitive(parent, "Runtime_DryingRack", PrimitiveType.Cube, localPosition, new Vector3(1.8f, 0.12f, 0.18f), SurvivalMat("StickBrown", new Color(0.32f, 0.18f, 0.08f)));
            EnsureNetworked(rack);
            DryingRack controller = Ensure<DryingRack>(rack);

            Transform[] anchors = new Transform[6];
            for (int i = 0; i < anchors.Length; i++)
            {
                GameObject anchor = new GameObject($"DrySlot_{i + 1}");
                anchor.transform.SetParent(rack.transform);
                anchor.transform.localPosition = new Vector3(-0.75f + i * 0.3f, -0.22f, 0f);
                GameObject meat = GetOrCreatePrimitive(anchor.transform, "FoodVisual", PrimitiveType.Cube, Vector3.zero, new Vector3(0.16f, 0.08f, 0.04f), SurvivalMat("MeatRed", new Color(0.45f, 0.06f, 0.04f)));
                meat.SetActive(false);
                anchors[i] = anchor.transform;
            }

            SerializedObject so = new SerializedObject(controller);
            SerializedProperty slots = so.FindProperty("slotVisualAnchors");
            slots.arraySize = anchors.Length;
            for (int i = 0; i < anchors.Length; i++) slots.GetArrayElementAtIndex(i).objectReferenceValue = anchors[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rack);
        }

        private static void CreateStickStorage(Transform parent, Vector3 localPosition)
        {
            GameObject storage = GetOrCreatePrimitive(parent, "Runtime_StickStorage", PrimitiveType.Cube, localPosition, new Vector3(1.1f, 0.7f, 0.55f), SurvivalMat("StickBrown", new Color(0.32f, 0.18f, 0.08f)));
            EnsureNetworked(storage);
            StorageContainer controller = Ensure<StorageContainer>(storage);

            SerializedObject so = new SerializedObject(controller);
            SerializedProperty accepted = so.FindProperty("acceptedItems");
            accepted.arraySize = 1;
            accepted.GetArrayElementAtIndex(0).objectReferenceValue = Item("stick");
            so.FindProperty("capacity").intValue = 40;
            so.FindProperty("displayName").stringValue = "Stick Storage";
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(storage);
        }

        private static GameObject GetOrCreatePrimitive(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return go;
        }

        private static void EnsureNetworked(GameObject go)
        {
            Ensure<NetworkObject>(go);
            Ensure<NetworkWorldObjectState>(go);
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static ItemData Item(string itemId)
        {
            ItemData[] catalog = Resources.LoadAll<ItemData>(ItemPath);
            foreach (ItemData item in catalog)
            {
                if (item != null && item.itemId == itemId) return item;
            }
            return null;
        }

        private static void SetObject(SerializedObject so, string property, Object value)
        {
            SerializedProperty prop = so.FindProperty(property);
            if (prop != null) prop.objectReferenceValue = value;
        }

        private static Material SurvivalMat(string name, Color color)
        {
            const string folder = "Assets/Generated/Materials";
            if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Generated", "Materials");

            string path = $"{folder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
