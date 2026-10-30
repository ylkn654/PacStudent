using UnityEditor;
using UnityEditor.Animations; // [NEW] namespace for animator controllers
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class BatchCreateAnimations : Editor
{
    [MenuItem("Assets/Batch Create Animations And Controllers (30FPS_every 2 frames)")]
    static void CreateAnimations()
    {
        Object[] selectedObjects = Selection.objects;
        int count = 0;

        foreach (Object obj in selectedObjects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            Texture2D texture = obj as Texture2D;
            if (texture == null) continue;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            List<Sprite> sprites = new List<Sprite>();
            foreach (Object asset in assets)
            {
                if (asset is Sprite sprite) sprites.Add(sprite);
            }

            if (sprites.Count == 0) continue;
            sprites = sprites.OrderBy(s => s.name).ToList();

            AnimationClip clip = new AnimationClip();
            clip.frameRate = 30;

            ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count + 1];
            for (int i = 0; i < sprites.Count; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe();
                keyframes[i].time = (i * 2) / 30f; 
                keyframes[i].value = sprites[i];
            }

            keyframes[sprites.Count] = new ObjectReferenceKeyframe();
            keyframes[sprites.Count].time = (sprites.Count * 2) / 30f;
            keyframes[sprites.Count].value = sprites[sprites.Count - 1];

            EditorCurveBinding binding = new EditorCurveBinding();
            binding.type = typeof(SpriteRenderer);
            binding.path = "";
            binding.propertyName = "m_Sprite";

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

            // [KEY 1] get the folder this image is in
            string directory = Path.GetDirectoryName(path);
            
            // final save paths for the clip and the controller
            string animSavePath = Path.Combine(directory, texture.name + "_Anim.anim").Replace("\\", "/");
            string controllerSavePath = Path.Combine(directory, texture.name + "_Controller.controller").Replace("\\", "/");

            // avoid errors: delete an old clip with the same name first
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(animSavePath) != null)
                AssetDatabase.DeleteAsset(animSavePath);
            
            // create and save the AnimationClip
            AssetDatabase.CreateAsset(clip, animSavePath);

            // avoid errors: delete an old controller with the same name first
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerSavePath) != null)
                AssetDatabase.DeleteAsset(controllerSavePath);

            // [KEY 2] create the Animator Controller
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerSavePath);
            
            // [KEY 3] add the new clip to the controller as its default state
            controller.AddMotion(clip);

            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Batch done! Created {count} clips and controllers, each saved next to its source image.");
    }
}