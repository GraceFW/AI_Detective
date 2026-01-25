using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Audio/SceneBGMMap")]
public class SceneBGMMapSO : ScriptableObject
{
    [Serializable]
    public class SceneEntry
    {
        public GameSceneSO scene;
        public BGMTrackSO track;
    }

    [Serializable]
    public class SceneTypeEntry
    {
        public SceneType sceneType;
        public BGMTrackSO track;
    }

    public List<SceneEntry> byScene = new List<SceneEntry>();
    public List<SceneTypeEntry> bySceneType = new List<SceneTypeEntry>();

    public bool TryGetTrack(GameSceneSO scene, out BGMTrackSO track)
    {
        if (scene != null)
        {
            for (int i = 0; i < byScene.Count; i++)
            {
                var e = byScene[i];
                if (e != null && e.scene == scene && e.track != null)
                {
                    track = e.track;
                    return true;
                }
            }

            for (int i = 0; i < bySceneType.Count; i++)
            {
                var e = bySceneType[i];
                if (e != null && e.sceneType == scene.sceneType && e.track != null)
                {
                    track = e.track;
                    return true;
                }
            }
        }

        track = null;
        return false;
    }
}
