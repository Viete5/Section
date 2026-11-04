using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Section.Sandbox.Editor
{
    public static class SandboxSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Dev/Sandbox_01.unity";
        public const string AssetRoot = "Assets/SECTION/Sandbox";
        public const string DefinitionPath = AssetRoot + "/Data/SandboxDefinition.asset";
        static readonly string[] stationNames = {
            "A1_GrabAndPlace", "A2_Weight", "A3_Orientation", "B1_Hypersphere", "B2_SlicePlatforms", "B3_DualSliceSensors",
            "C1_Tesseract", "C2_NarrowAperture", "C3_4DConnector", "D1_SpatialGate", "D2_MovingWalls", "D3_CrossSliceContact"
        };
        public static IReadOnlyList<string> StationNames => stationNames;

        [MenuItem("SECTION/Sandbox 01/Build or Validate Sandbox Scene")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring Sandbox 01.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Unsaved scene changes found. Save or review them before generating Sandbox 01.");

            Scene scene;
            bool existed = File.Exists(ScenePath);
            if (existed)
            {
                scene = SceneManager.GetSceneByPath(ScenePath);
                if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                SceneManager.SetActiveScene(scene);
                var owned = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SandboxGeneratedObject>(true)).ToArray();
                if (owned.Length == 0) throw new InvalidOperationException("Existing Sandbox scene is not owned by this builder. Nothing was overwritten.");
                var edits = owned.Where(t => t.Baseline != Snapshot(t)).Select(t => t.Key).ToArray();
                if (edits.Length > 0)
                    throw new InvalidOperationException("Manual changes in generated objects; regeneration refused. Review: " + string.Join(", ", edits));
            }
            else scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Directory.CreateDirectory("Assets/Scenes/Dev");
            Directory.CreateDirectory(AssetRoot + "/Data");
            Directory.CreateDirectory(AssetRoot + "/Materials");
            AssetDatabase.Refresh();
            var config = AssetDatabase.LoadAssetAtPath<SandboxDefinition>(DefinitionPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<SandboxDefinition>();
                AssetDatabase.CreateAsset(config, DefinitionPath);
            }
            config.ValidateApprovedGeometry();
            var floor = Material("Floor", new Color(.39f, .42f, .44f));
            var wall = Material("Wall", new Color(.62f, .65f, .66f));
            var ceiling = Material("Ceiling", new Color(.52f, .55f, .57f));
            var context = new AuthoringContext(scene);
            Transform root = context.Node("root", "Sandbox_01", null);
            Transform environment = context.Node("environment", "Environment", root);
            Transform shell = context.Node("shell", "Shell_3D", environment);
            Transform partitions = context.Node("partitions", "Partitions_3D", environment);
            Transform pit = context.Node("pit", "B2_FloorOpening", environment);
            Transform lighting = context.Node("lighting", "Lighting", environment);

            context.Box("floor.a", "Floor_A", shell, new Vector3(0, -.15f, 5), new Vector3(14, .3f, 10), floor);
            context.Box("floor.c", "Floor_C", shell, new Vector3(0, -.15f, 25), new Vector3(14, .3f, 10), floor);
            context.Box("floor.d", "Floor_D", shell, new Vector3(0, -.15f, 35), new Vector3(14, .3f, 10), floor);
            // Four disjoint rectangles form B's floor. No invisible full-room collider remains.
            context.Box("floor.b.left", "Floor_B_Left", shell, new Vector3(-2.25f, -.15f, 15), new Vector3(9.5f, .3f, 10), floor);
            context.Box("floor.b.right", "Floor_B_Right", shell, new Vector3(6.5f, -.15f, 15), new Vector3(1, .3f, 10), floor);
            context.Box("floor.b.front", "Floor_B_NearBank", shell, new Vector3(4.25f, -.15f, 11.5f), new Vector3(3.5f, .3f, 3), floor);
            context.Box("floor.b.back", "Floor_B_FarBank", shell, new Vector3(4.25f, -.15f, 19), new Vector3(3.5f, .3f, 2), floor);
            context.Box("ceiling", "Ceiling", shell, new Vector3(0, 5.15f, 20), new Vector3(14.6f, .3f, 40.6f), ceiling);
            context.Box("wall.left", "Wall_Left", shell, new Vector3(-7.15f, 2.5f, 20), new Vector3(.3f, 5, 40.6f), wall);
            context.Box("wall.right", "Wall_Right", shell, new Vector3(7.15f, 2.5f, 20), new Vector3(.3f, 5, 40.6f), wall);
            context.Box("wall.start", "Wall_Start", shell, new Vector3(0, 2.5f, -.15f), new Vector3(14, 5, .3f), wall);
            context.Box("wall.end", "Wall_End", shell, new Vector3(0, 2.5f, 40.15f), new Vector3(14, 5, .3f), wall);
            for (int i = 1; i <= 3; i++)
                foreach (int side in new[] { -1, 1 })
                    context.Box("partition." + i + "." + side, "Partition_" + i + (side < 0 ? "_Left" : "_Right"), partitions,
                        new Vector3(side * 4.25f, 2.5f, i * 10), new Vector3(5.5f, 5, .3f), wall);

            context.Box("pit.bottom", "Pit_Bottom", pit, new Vector3(4.25f, -2.65f, 15.5f), new Vector3(3.5f, .3f, 5), floor);
            // The lining meets the underside of the floor; it neither narrows the opening nor protrudes above it.
            context.Box("pit.left", "Pit_Left", pit, new Vector3(2.35f, -1.4f, 15.5f), new Vector3(.3f, 2.2f, 5), wall);
            context.Box("pit.right", "Pit_Right", pit, new Vector3(6.15f, -1.4f, 15.5f), new Vector3(.3f, 2.2f, 5), wall);
            context.Box("pit.front", "Pit_Near", pit, new Vector3(4.25f, -1.4f, 12.85f), new Vector3(3.5f, 2.2f, .3f), wall);
            context.Box("pit.back", "Pit_Far", pit, new Vector3(4.25f, -1.4f, 18.15f), new Vector3(3.5f, 2.2f, .3f), wall);

            for (int i = 0; i < 4; i++)
            {
                var fixture = context.Node("light." + i, "Room_" + (char)('A' + i) + "_Light", lighting, new Vector3(0, 4.55f, 5 + i * 10));
                var lamp = context.Component<Light>(fixture);
                context.Component<UniversalAdditionalLightData>(fixture);
                lamp.type = LightType.Point; lamp.color = new Color(.94f, .97f, 1); lamp.intensity = 6;
                lamp.range = 12; lamp.shadows = LightShadows.Soft; lamp.shadowBias = .03f;
                lamp.shadowNormalBias = .2f;
                context.Box("light.panel." + i, "LightPanel_" + (char)('A' + i), lighting,
                    new Vector3(0, 4.96f, 5 + i * 10), new Vector3(1.6f, .06f, .45f), ceiling, false);
            }
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.62f, .65f, .67f);
            RenderSettings.fog = false;

            Transform playerRoot = context.Node("player.root", "Player", root);
            Transform player = context.Node("player.motor", "FirstPersonController", playerRoot, config.spawnFeet);
            var controller = context.Component<CharacterController>(player);
            controller.height = config.playerHeight; controller.radius = config.playerRadius;
            controller.center = Vector3.up * config.playerHeight * .5f;
            controller.skinWidth = .02f; controller.stepOffset = .25f; controller.slopeLimit = 45;
            controller.minMoveDistance = 0;
            var input = context.Component<SandboxPlayerInput>(player);
            var motor = context.Component<SandboxMotor>(player); motor.Configure(config, input);
            Transform cameraRoot = context.Node("player.camera", "PlayerCamera", player, Vector3.up * config.eyeHeight);
            cameraRoot.tag = "MainCamera";
            var camera = context.Component<Camera>(cameraRoot);
            camera.fieldOfView = 70; camera.nearClipPlane = .05f; camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.22f, .25f, .27f);
            context.Component<AudioListener>(cameraRoot);
            var cameraData = context.Component<UniversalAdditionalCameraData>(cameraRoot);
            cameraData.renderPostProcessing = false;
            var look = context.Component<SandboxLook>(player); look.Configure(config, input, player, cameraRoot);
            var anchor = context.Node("player.anchor", "EquipmentAnchor", cameraRoot);
            context.Node("player.slots", "InventorySlots", playerRoot);

            Transform stations = context.Node("stations", "Stations", root);
            // Stable roots only. No devices, station props, sensors, or success flags exist in M1.
            foreach (string name in stationNames) context.Node("station." + name.Substring(0, 2), name, stations);
            Transform developer = context.Node("developer", "DeveloperOnly", root);
            context.Node("developer.diagnostics", "Diagnostics", developer);
            context.Node("developer.localReset", "LocalResets", developer);
            context.Node("developer.globalReset", "GlobalReset", developer);
            Transform bootstrap = context.Node("bootstrap", "SandboxBootstrap", root);
            context.Component<SandboxScene>(bootstrap).Configure(config, motor, look);

            SandboxInteractionBuilder.Populate(context, player, input, cameraRoot, anchor, look);
            SandboxGeometryBuilder.Populate(context);
            SandboxWorldShiftBuilder.Populate(context,player,input,cameraRoot,anchor,look);
            SandboxDimensionBuilder.Populate(context,player,input,cameraRoot,anchor,look);
            SandboxIntegrationBuilder.Populate(context,player,look);
            context.AssertAllManagedObjectsUsed();
            foreach (var tag in context.Tags) tag.RecordBaseline(Snapshot(tag));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save " + ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("SECTION Sandbox M3 saved: M1/M2 preserved; computed B1/C1 geometry, diagnostics only.");
        }

        internal static Material Material(string name, Color color)
        {
            string path = AssetRoot + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader unavailable.");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", .15f);
            AssetDatabase.CreateAsset(material, path); return material;
        }

        public static SandboxGeneratedObject Find(Scene scene, string key) => scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<SandboxGeneratedObject>(true)).Single(t => t.Key == key);

        public static string Snapshot(SandboxGeneratedObject tag)
        {
            string F(float n) => n.ToString("R", CultureInfo.InvariantCulture);
            string V(Vector3 v) => F(v.x) + "," + F(v.y) + "," + F(v.z);
            string Ref(Component c) => c == null ? "null" : c.GetComponent<SandboxGeneratedObject>()?.Key ?? c.name;
            var t = tag.transform; var q = t.localRotation;
            var data = new List<string> { tag.name, t.parent == null ? "root" : Ref(t.parent), V(t.localPosition),
                F(q.x) + "," + F(q.y) + "," + F(q.z) + "," + F(q.w), V(t.localScale), tag.gameObject.activeSelf.ToString() };
            foreach (var component in tag.GetComponents<Component>().OrderBy(c => c == null ? "" : c.GetType().FullName))
            {
                if (component == null) { data.Add("MissingScript"); continue; }
                data.Add(component.GetType().FullName);
                if (component is BoxCollider b) data.Add(V(b.center) + ":" + V(b.size) + ":" + b.isTrigger + ":" + b.enabled);
                if (component is CharacterController cc) data.Add(V(cc.center) + ":" + F(cc.height) + ":" + F(cc.radius) + ":" + F(cc.skinWidth) + ":" + F(cc.stepOffset));
                if (component is MeshRenderer renderer) data.Add(renderer.enabled + ":" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(renderer.sharedMaterial)));
                if (component is MeshFilter filter) data.Add(filter.sharedMesh == null ? "null" : filter.sharedMesh.name);
                if (component is Light l) data.Add(l.type + ":" + V(new Vector3(l.color.r, l.color.g, l.color.b)) + ":" + F(l.intensity) + ":" + F(l.range) + ":" + l.shadows);
                if (component is Camera c) data.Add(F(c.fieldOfView) + ":" + F(c.nearClipPlane) + ":" + F(c.farClipPlane));
                if (component is SandboxMotor m) data.Add(Ref(m.Input) + ":" + AssetDatabase.GetAssetPath(m.Definition));
                if (component is SandboxLook look) data.Add(Ref(look.Input) + ":" + Ref(look.Body) + ":" + Ref(look.View) + ":" + AssetDatabase.GetAssetPath(look.Definition));
                if (component is SandboxScene scene) data.Add(Ref(scene.Player) + ":" + Ref(scene.Look) + ":" + AssetDatabase.GetAssetPath(scene.Definition));
                if (component is PhysicalItem || component is StandardManipulator || component is TestStation ||
                    component is StationCondition || component is StationResetButton || component is InteractionHud ||
                    component is WorldSliceState || component is WorldShiftDevice || component is EquipmentSlots ||
                    component is StationInteractor || component is GeometryResetHandler || component is SlicePresenceSensor ||
                    component is FourDimensionalBody || component is FourDimensionalPhysics || component is DimensionManipulator || component is OrdinaryContactProjection ||
                    component is UnityEngine.UIElements.UIDocument)
                {
                    using var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.NextVisible(true))
                    {
                        if (property.propertyPath == "m_Script") continue;
                        if(component is MovingWallsCondition && property.propertyPath.StartsWith("phaseLamps") && serialized.FindProperty("phaseLamps").arraySize==0)continue;
                        if(component is OrdinaryContactProjection && property.name=="partIndex" && property.intValue==0)continue;
                        if(property.name=="dimension" && property.propertyType==SerializedPropertyType.ObjectReference && property.objectReferenceValue==null)continue;
                        // Schema migration: absent M4 references/default button action preserve the old M2 baseline.
                        if((property.name=="geometryReset" || component is InteractionHud &&
                            (property.name=="shift" || property.name=="interactor" || property.name=="input")) &&
                            property.propertyType==SerializedPropertyType.ObjectReference && property.objectReferenceValue==null)continue;
                        if(component is StationResetButton && property.name=="begin" && !property.boolValue)continue;
                        string value;
                        if (property.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            var reference = property.objectReferenceValue;
                            string path = AssetDatabase.GetAssetPath(reference);
                            value = reference == null ? "null" : !string.IsNullOrEmpty(path) ? AssetDatabase.AssetPathToGUID(path) :
                                reference is Component referenceComponent ? Ref(referenceComponent) : reference.name;
                        }
                        else value = property.propertyType switch {
                            SerializedPropertyType.String => property.stringValue,
                            SerializedPropertyType.Float => F(property.floatValue),
                            SerializedPropertyType.Integer or SerializedPropertyType.Enum => property.intValue.ToString(),
                            SerializedPropertyType.Boolean => property.boolValue.ToString(),
                            SerializedPropertyType.Vector3 => V(property.vector3Value),
                            SerializedPropertyType.Quaternion => property.quaternionValue.ToString("R"),
                            _ => property.propertyType.ToString()
                        };
                        data.Add(property.propertyPath + "=" + value);
                    }
                }
                if (component is GeometrySliceView geometry) data.Add(geometry.ConfigurationSignature);
                if (component is GeometryDiagnostics diagnostics) data.Add(diagnostics.ConfigurationSignature);
                if (component is Rigidbody rb) data.Add(F(rb.mass) + ":" + rb.useGravity + ":" + rb.isKinematic + ":" + rb.detectCollisions + ":" + rb.collisionDetectionMode);
                if (component is UniversalAdditionalCameraData cameraData) data.Add(cameraData.renderPostProcessing.ToString());
            }
            if (tag.Key == "root") data.Add(RenderSettings.ambientMode + ":" + RenderSettings.ambientLight + ":" + RenderSettings.fog);
            return string.Join("|", data);
        }

        internal sealed class AuthoringContext
        {
            readonly Dictionary<string, SandboxGeneratedObject> tags;
            readonly HashSet<string> used = new HashSet<string>();
            public IEnumerable<SandboxGeneratedObject> Tags => tags.Values;
            public AuthoringContext(Scene scene)
            {
                tags = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SandboxGeneratedObject>(true)).ToDictionary(t => t.Key);
            }
            public Transform Node(string key, string name, Transform parent, Vector3 position = default)
            {
                if (!tags.TryGetValue(key, out var tag))
                {
                    var go = new GameObject(name); tag = go.AddComponent<SandboxGeneratedObject>(); tag.SetKey(key); tags.Add(key, tag);
                }
                used.Add(key);
                var t = tag.transform; t.SetParent(parent, false); t.name = name;
                t.localPosition = position; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
                return t;
            }
            public T Component<T>(Transform t) where T : Component
            {
                // Editor GetComponent can return Unity's missing-component wrapper: use Unity's null check.
                var component = t.GetComponent<T>();
                return component != null ? component : t.gameObject.AddComponent<T>();
            }
            public Transform Box(string key, string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collision = true)
            {
                var t = Node(key, name, parent, position); t.localScale = size;
                var mesh = Component<MeshFilter>(t);
                if (mesh.sharedMesh == null)
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    mesh.sharedMesh = cube.GetComponent<MeshFilter>().sharedMesh;
                    UnityEngine.Object.DestroyImmediate(cube);
                }
                Component<MeshRenderer>(t).sharedMaterial = material;
                if (collision) { var c = Component<BoxCollider>(t); c.center = Vector3.zero; c.size = Vector3.one; }
                return t;
            }
            public void AssertAllManagedObjectsUsed()
            {
                var unknown = tags.Keys.Where(k => !used.Contains(k)).ToArray();
                if (unknown.Length > 0) throw new InvalidOperationException("Unknown managed IDs require a migration, not deletion: " + string.Join(",", unknown));
            }
        }
    }
}
