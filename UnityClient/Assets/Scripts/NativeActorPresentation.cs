using UnityEngine;

// Explicit client-owned visual root. This is not the official Actor component.
public sealed class NativeActorPresentation : MonoBehaviour
{
    public GameObject VisualRoot{get;private set;}
    public bool Shown{get;private set;}
    public void Configure(GameObject root){VisualRoot=root;Shown=root!=null&&root.activeSelf;}
    public void SetShown(bool visible){Shown=visible;if(VisualRoot!=null)VisualRoot.SetActive(visible);}
    public static bool IsActorShown(GameObject model)
    {
        if(model==null)return false;
        var presentation=model.GetComponentInChildren<NativeActorPresentation>(true);
        return presentation==null||presentation.Shown;
    }
}
