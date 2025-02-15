using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    public static class CreateStampMenu
    {
        static GameObject CreateStampGameObject(string name, Type stampType, MenuCommand menuCommand, Spline spline = null)
        {
            var uniqueName = GameObjectUtility.GetUniqueNameForSibling(null, $"{name}");
            var gameObject = ObjectFactory.CreateGameObject(uniqueName, typeof(Stamp), stampType);

#if UNITY_2022_1_OR_NEWER
            ObjectFactory.PlaceGameObject(gameObject, menuCommand.context as GameObject);
#else
            if (menuCommand.context is GameObject go)
            {
                Undo.RecordObject(gameObject.transform, "Re-parenting");
                gameObject.transform.SetParent(go.transform);
            }
#endif
            if (spline != null)
            {
                var container = gameObject.GetComponent<SplineContainer>();
                container.Spline = spline;
            }
            Selection.activeGameObject = gameObject;
            return gameObject;
        }

        private const int kMenuPriority = 2;
        private const float kMenuSecondaryPriority = 0.5f;
        [MenuItem("GameObject/WorldBuilding Stamps/Circle", priority = kMenuPriority, secondaryPriority = kMenuSecondaryPriority)]
        static void CreateCircleStamp(MenuCommand menuCommand)
        {
            CreateStampGameObject("Circle Stamp", typeof(CircleShape), menuCommand);
        }
        
        [MenuItem("GameObject/WorldBuilding Stamps/Rectangle", priority = kMenuPriority, secondaryPriority = kMenuSecondaryPriority)]
        static void CreateRectangleStamp(MenuCommand menuCommand)
        {
            CreateStampGameObject("Rectangle Stamp", typeof(RectangleShape), menuCommand);
        }
        
        [MenuItem("GameObject/WorldBuilding Stamps/Global", priority = kMenuPriority, secondaryPriority = kMenuSecondaryPriority)]
        static void CreateGlobalStamp(MenuCommand menuCommand)
        {
            CreateStampGameObject("Global Stamp", typeof(GlobalShape), menuCommand);
        }
        
        [MenuItem("GameObject/WorldBuilding Stamps/Spline Path", priority = kMenuPriority, secondaryPriority = kMenuSecondaryPriority)]
        static void CreateSplinePathStamp(MenuCommand menuCommand)
        {
            Vector3 forward = SceneView.lastActiveSceneView.rotation * Vector3.forward;
            forward.y = 0;
            forward.Normalize();
            CreateStampGameObject("Spline Path Stamp", typeof(SplinePathShape), menuCommand, SplineFactory.CreateLinear(new float3[] { -forward * 5.0f, Vector3.zero, forward * 5.0f }));
        }
        
        [MenuItem("GameObject/WorldBuilding Stamps/Spline Area", priority = kMenuPriority, secondaryPriority = kMenuSecondaryPriority)]
        static void CreateSplineAreaStamp(MenuCommand menuCommand)
        {
            CreateStampGameObject("Spline Path Area", typeof(SplineAreaShape), menuCommand, SplineFactory.CreateRoundedCornerSquare(10.0f, 3.0f));
        }
    }
}