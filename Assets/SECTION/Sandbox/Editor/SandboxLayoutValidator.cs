using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Section.Sandbox.Editor
{
    public static class SandboxLayoutValidator
    {
        public static List<string> Validate(Scene scene)
        {
            var errors = new List<string>();
            var roots = scene.GetRootGameObjects();
            var bindings = roots.SelectMany(g => g.GetComponentsInChildren<SandboxScene>(true)).ToArray();
            if (bindings.Length != 1) errors.Add("Exactly one SandboxScene binding is required.");
            else
            {
                var b = bindings[0];
                if (b.Definition == null || b.Player == null || b.Look == null) errors.Add("Missing explicit scene references.");
                else
                {
                    try { b.Definition.ValidateApprovedGeometry(); }
                    catch (System.InvalidOperationException exception) { errors.Add(exception.Message); }
                    var cc = b.Player.Controller;
                    if (!Near(cc.height, 1.8f) || !Near(cc.radius, .35f) || !Near(cc.center.y, .9f)) errors.Add("Incorrect player capsule.");
                    if (!Near(b.Look.View.localPosition.y, 1.65f)) errors.Add("Incorrect camera height.");
                }
            }
            var tags = roots.SelectMany(g => g.GetComponentsInChildren<SandboxGeneratedObject>(true)).ToArray();
            foreach (var group in tags.GroupBy(t => t.Key))
                if (string.IsNullOrEmpty(group.Key) || group.Count() != 1) errors.Add("Duplicate/empty authoring ID: " + group.Key);
            foreach (var tag in tags)
                if (tag.Baseline != SandboxSceneBuilder.Snapshot(tag)) errors.Add("Managed object differs from recorded baseline: " + tag.Key);
            foreach (string name in SandboxSceneBuilder.StationNames)
                if (tags.Count(t => t.Key == "station." + name.Substring(0, 2)) != 1) errors.Add("Missing station root: " + name);
            foreach (var go in roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)))
                if (UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go.gameObject) > 0) errors.Add("Missing script: " + go.name);
            return errors;
        }
        static bool Near(float a, float b) => Mathf.Abs(a - b) < .0001f;
    }
}
