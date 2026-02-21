using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpdatableData : ScriptableObject
{
    public event System.Action OnValuesUpdated;
    public bool autoUpdate;

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        if (autoUpdate)
        {
            UnityEditor.EditorApplication.update -= NotifyOfUpdatedValues;
            UnityEditor.EditorApplication.update += NotifyOfUpdatedValues;
        }
        else
        {
            UnityEditor.EditorApplication.update -= NotifyOfUpdatedValues;
        }
    }

    protected virtual void OnDisable()
    {
        UnityEditor.EditorApplication.update -= NotifyOfUpdatedValues;
    }

    public void NotifyOfUpdatedValues()
    {
        UnityEditor.EditorApplication.update -= NotifyOfUpdatedValues;
        if (OnValuesUpdated != null)
        {
            OnValuesUpdated();
        }
    }
#endif
}
