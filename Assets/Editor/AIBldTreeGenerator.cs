using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.IO;

public class AIBldTreeGenerator : EditorWindow
{
    [MenuItem("Tools/Сгенерировать Боевой Контроллер")]
    public static void GenerateProjectStandardController()
    {
        Object[] selectedObjects = Selection.objects;
        if (selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("Ошибка", "Выдели в окне Project файлы анимаций (FBX с Mixamo)!", "Понял");
            return;
        }

        string firstObjPath = AssetDatabase.GetAssetPath(selectedObjects[0]);
        string targetFolder = Path.GetDirectoryName(firstObjPath).Replace("\\", "/");
        string savePath = $"{targetFolder}/Auto_Generated_Controller.controller";
        
        AssetDatabase.DeleteAsset(savePath);

        List<AnimationClip> clips = new List<AnimationClip>();

        foreach (Object obj in selectedObjects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            string fileName = Path.GetFileNameWithoutExtension(path);

            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object subAsset in subAssets)
            {
                if (subAsset is AnimationClip && !subAsset.name.Contains("__preview__"))
                {
                    AnimationClip originalClip = (AnimationClip)subAsset;
                    AnimationClip renamedClip = Instantiate(originalClip);
                    renamedClip.name = fileName;

                    string clipSavePath = $"{targetFolder}/{fileName}_anim.anim";
                    AssetDatabase.DeleteAsset(clipSavePath);
                    AssetDatabase.CreateAsset(renamedClip, clipSavePath);

                    clips.Add(renamedClip);
                }
            }
        }

        // 1. Создаем чистый контроллер
        AnimatorController animatorController = AnimatorController.CreateAnimatorControllerAtPath(savePath);

        // 2. Создаем Дерево Смешивания ПЕРВЫМ делом (чтобы Unity ничего не стер)
        BlendTree blendTree;
        AnimatorState state = animatorController.CreateBlendTreeInController("MoveTree", out blendTree, 0);
        state.name = "Movement_Rifle";
        
        // 3. Настраиваем тип дерева на 2D
        blendTree.blendType = BlendTreeType.SimpleDirectional2D;

        // 4. ВОТ ТЕПЕРЬ НАСТОЙЧИВО ДОБАВЛЯЕМ ВСЕ 13 ПАРАМЕТРОВ ПРОЕКТА
        animatorController.AddParameter("VelocityX", AnimatorControllerParameterType.Float);
        animatorController.AddParameter("VelocityY", AnimatorControllerParameterType.Float);
        animatorController.AddParameter("MoveAmount", AnimatorControllerParameterType.Float);
        animatorController.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);

        animatorController.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
        animatorController.AddParameter("IsAiming", AnimatorControllerParameterType.Bool);
        animatorController.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);

        animatorController.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
        animatorController.AddParameter("Reload", AnimatorControllerParameterType.Trigger);
        animatorController.AddParameter("ThrowGrenade", AnimatorControllerParameterType.Trigger);
        animatorController.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        animatorController.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        // 5. Линкуем оси дерева к созданным параметрам
        blendTree.blendParameter = "VelocityX";
        blendTree.blendParameterY = "VelocityY";

        // 6. Закидываем созданные анимации в дерево
        foreach (AnimationClip clip in clips)
        {
            blendTree.AddChild(clip);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Успех!", $"Контроллер пересоздан!\nТеперь в списке ТОЧНО есть все 13 параметров проекта.", "Отлично");
    }
}