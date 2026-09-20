using UnityEditor;
using UnityEditor.Animations; // 【新增】引入动画控制器的命名空间
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class BatchCreateAnimations : Editor
{
    [MenuItem("Assets/一键批量生成动画及控制器 (30FPS_间隔2帧)")]
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

            // 【关键点1】获取这张图片自身所在的文件夹路径
            string directory = Path.GetDirectoryName(path);
            
            // 设定动画和控制器的最终保存路径
            string animSavePath = Path.Combine(directory, texture.name + "_Anim.anim").Replace("\\", "/");
            string controllerSavePath = Path.Combine(directory, texture.name + "_Controller.controller").Replace("\\", "/");

            // 防报错：如果存在同名的旧动画文件，先删除
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(animSavePath) != null)
                AssetDatabase.DeleteAsset(animSavePath);
            
            // 创建并保存 AnimationClip 文件
            AssetDatabase.CreateAsset(clip, animSavePath);

            // 防报错：如果存在同名的旧控制器文件，先删除
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerSavePath) != null)
                AssetDatabase.DeleteAsset(controllerSavePath);

            // 【关键点2】自动创建 Animator Controller
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerSavePath);
            
            // 【关键点3】将刚刚生成的动画自动添加到控制器中，作为默认的播放状态(Default State)
            controller.AddMotion(clip);

            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"批量生成完毕！共生成了 {count} 组动画和控制器文件，并已分别存入对应的源文件夹中。");
    }
}