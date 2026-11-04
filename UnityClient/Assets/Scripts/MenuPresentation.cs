using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 使用木盒的原始相机和按钮网格定位，避开背景平面造成的整体缩放。
public sealed class MenuPresentation
{
    struct FooterTransformState
    {
        public Transform transform;
        public Vector3 localPosition;
        public Vector3 localScale;
        public bool active;
        public Renderer renderer;
        public bool rendererEnabled;
    }

    readonly GameObject root;
    readonly Camera camera;
    readonly Transform[] nodes;
    readonly SceneCamera settings;
    readonly List<FooterTransformState> footerBaseline=new List<FooterTransformState>();
    bool footerBaselineCaptured;
    DesignViewport viewport;
    readonly System.Collections.Generic.Dictionary<Material,Color> colors=new System.Collections.Generic.Dictionary<Material,Color>();
    public MenuPresentation(GameObject scene, Camera view, SceneData data)
    {
        root=scene;camera=view;nodes=root.GetComponentsInChildren<Transform>(true);
        var original=nodes.FirstOrDefault(n=>n.name=="TheBox");
        if(original!=null)root.transform.localScale=Vector3.one/original.localScale.x;
        settings=data.nodes.FirstOrDefault(n=>n.name=="TheBoxCamera")?.camera;
        foreach(var node in nodes)
        {
            if(node.name=="BlackFillQuad"||node.name=="FakeLidShadowQuad"||node.name=="StoreClosed"||
               node.name=="StoreClosedText"||node.name=="Logo"||node.name=="JournalButton"||
               node.name=="Shop_Coin_shadow"||
               node.name.StartsWith("Highlight_")||node.name.IndexOf("glow",StringComparison.OrdinalIgnoreCase)>=0||
               node.name.Contains("GodRay")||node.name=="FX_GodRays1"||node.name=="TheBox_TracerComet"||
               node.name=="TheBox_SpotLightHaze"||node.name=="TheBox_InsideGlow"||node.name=="GlowRingQuad1")
                node.gameObject.SetActive(false);
        }
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            foreach(var material in renderer.sharedMaterials)colors[material]=material.GetColor("_Tint");
    }
    public void Show()
    {
        Show(DesignViewport.Menu);
    }
    public void Show(DesignViewport design)
    {
        RestoreHubFooterLayout();
        viewport=design;viewport.Apply(camera);
        var pose=nodes.FirstOrDefault(n=>n.name=="CameraClosed");
        if(pose==null)return;
        camera.transform.SetPositionAndRotation(pose.position,pose.rotation);
        camera.fieldOfView=settings!=null?settings.field_of_view:20.7f;
        camera.nearClipPlane=.1f;camera.farClipPlane=200;
    }
    public Rect ButtonRect(string name)
    {
        var node=FindNode(name);
        if(node==null)return Rect.zero;
        var renderers=node.GetComponentsInChildren<Renderer>();
        return ProjectedRect(renderers);
    }
    public void LayoutHubFooter()
    {
        if(root==null||camera==null||viewport.Width<1||viewport.Height<1)return;
        // F024 opts in explicitly; Show restores source transforms for the legacy/editor view.
        var tray=FindNode("TheBox_TrayPacksCards");
        var store=FindNode("Button_Store");
        var packs=FindNode("Button_OpenPacks_mesh");
        var collection=FindNode("Button_Collection_mesh");
        var quests=FindNode("QuestLogButton");
        var packCount=FindNode("PackCount_mesh");
        var shopNotice=FindNode("ShopNewNotice");
        CaptureFooterBaseline(tray,packs,collection,store,quests,packCount,shopNotice);

        if(tray!=null)tray.gameObject.SetActive(true);
        if(quests!=null)quests.gameObject.SetActive(true);
        if(packCount!=null)packCount.gameObject.SetActive(false);
        if(shopNotice!=null)shopNotice.gameObject.SetActive(false);
        float foregroundDepth=FindWoodFrontDepth();
        MoveNodeToForeground(tray,true,foregroundDepth);
        MoveNodeToForeground(store,true,foregroundDepth);
        MoveNodeToForeground(quests,true,foregroundDepth);
        var trayRenderer=tray==null?null:tray.GetComponent<Renderer>();
        if(trayRenderer!=null)
        {
            Rect trayRect=ProjectedRect(new[]{trayRenderer});
            MoveNodeToDesignCenter(tray,new Vector2(trayRect.center.x,925),false);
            trayRenderer.enabled=false;
        }
        MoveNodeToDesignCenter(quests,new Vector2(360,925),true);
        MoveNodeToDesignCenter(store,new Vector2(540,925),true);
        MoveNodeToDesignCenter(packs,new Vector2(730,925),true);
        MoveNodeToDesignCenter(collection,new Vector2(1050,925),true);
    }
    public void RestoreHubFooterLayout()
    {
        if(!footerBaselineCaptured)return;
        foreach(var state in footerBaseline)
        {
            if(state.transform==null)continue;
            state.transform.localPosition=state.localPosition;
            state.transform.localScale=state.localScale;
            state.transform.gameObject.SetActive(state.active);
            if(state.renderer!=null)state.renderer.enabled=state.rendererEnabled;
        }
        footerBaseline.Clear();
        footerBaselineCaptured=false;
    }
    Transform FindNode(string name)
    {
        return nodes.FirstOrDefault(n=>n.name==name);
    }
    void CaptureFooterBaseline(params Transform[] footerNodes)
    {
        if(footerBaselineCaptured)return;
        footerBaseline.Clear();
        foreach(var node in footerNodes)
        {
            if(node==null)continue;
            var renderer=node.GetComponent<Renderer>();
            footerBaseline.Add(new FooterTransformState
            {
                transform=node,
                localPosition=node.localPosition,
                localScale=node.localScale,
                active=node.gameObject.activeSelf,
                renderer=renderer,
                rendererEnabled=renderer!=null&&renderer.enabled
            });
        }
        footerBaselineCaptured=true;
    }
    float FindWoodFrontDepth()
    {
        float nearest=float.PositiveInfinity;
        foreach(var name in new[]{"TheBox_Outer","TheBox_OuterFrame"})
        {
            var branch=FindNode(name);
            if(branch==null)continue;
            foreach(var renderer in branch.GetComponentsInChildren<Renderer>())
            {
                if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                Bounds bounds=renderer.bounds;
                for(int i=0;i<8;i++)
                {
                    Vector3 corner=bounds.center+Vector3.Scale(bounds.extents,
                        new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    float depth=camera.WorldToScreenPoint(corner).z;
                    if(depth>camera.nearClipPlane&&depth<nearest)nearest=depth;
                }
            }
        }
        if(float.IsInfinity(nearest))
        {
            float boxDepth=camera.WorldToScreenPoint(root.transform.position).z;
            nearest=boxDepth-3f;
        }
        return Mathf.Max(camera.nearClipPlane+.5f,nearest-.3f);
    }
    void MoveNodeToForeground(Transform node,bool includeChildren,float targetDepth)
    {
        if(node==null)return;
        var renderers=includeChildren?node.GetComponentsInChildren<Renderer>():node.GetComponents<Renderer>();
        if(renderers.Length==0)return;
        Bounds bounds=renderers[0].bounds;
        foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        float oldDepth=camera.WorldToScreenPoint(bounds.center).z;
        if(oldDepth<=camera.nearClipPlane)return;
        float farthestDepth=oldDepth;
        for(int i=0;i<8;i++)
        {
            Vector3 corner=bounds.center+Vector3.Scale(bounds.extents,
                new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
            farthestDepth=Mathf.Max(farthestDepth,camera.WorldToScreenPoint(corner).z);
        }
        if(farthestDepth<=targetDepth)return;
        float targetCenterDepth=targetDepth*oldDepth/farthestDepth;
        if(targetCenterDepth>=oldDepth)return;

        float scaleFactor=targetCenterDepth/oldDepth;
        node.position+=camera.transform.forward*(targetCenterDepth-oldDepth);
        node.localScale*=scaleFactor;
        bounds=renderers[0].bounds;
        foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        float scaledDepth=camera.WorldToScreenPoint(bounds.center).z;
        node.position+=camera.transform.forward*(targetCenterDepth-scaledDepth);
    }
    Rect ProjectedRect(Renderer[] renderers)
    {
        if(renderers==null||renderers.Length==0)return Rect.zero;
        var bounds=renderers[0].bounds;
        foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=-min;
        for(int i=0;i<8;i++)
        {
            var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
            var ui=viewport.Project(camera,corner);
            min=Vector2.Min(min,ui);max=Vector2.Max(max,ui);
        }
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
    void MoveNodeToDesignCenter(Transform node,Vector2 target,bool includeChildren)
    {
        if(node==null)return;
        var renderers=includeChildren?node.GetComponentsInChildren<Renderer>():node.GetComponents<Renderer>();
        if(renderers.Length==0)return;
        Rect rect=ProjectedRect(renderers);
        if(rect.width<=0||rect.height<=0)return;
        Rect safe=new Rect(18,750,1564,230);
        float halfWidth=Mathf.Min(rect.width*.5f,safe.width*.5f);
        float halfHeight=Mathf.Min(rect.height*.5f,safe.height*.5f);
        target.x=Mathf.Clamp(target.x,safe.xMin+halfWidth,safe.xMax-halfWidth);
        target.y=Mathf.Clamp(target.y,safe.yMin+halfHeight,safe.yMax-halfHeight);

        var bounds=renderers[0].bounds;
        foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        float depth=camera.WorldToScreenPoint(bounds.center).z;
        if(depth<=.01f)return;
        Vector3 currentPixel=viewport.Matrix.MultiplyPoint3x4(new Vector3(rect.center.x,rect.center.y,0));
        Vector3 targetPixel=viewport.Matrix.MultiplyPoint3x4(new Vector3(target.x,target.y,0));
        Vector3 currentWorld=camera.ScreenToWorldPoint(new Vector3(currentPixel.x,viewport.WindowHeight-currentPixel.y,depth));
        Vector3 targetWorld=camera.ScreenToWorldPoint(new Vector3(targetPixel.x,viewport.WindowHeight-targetPixel.y,depth));
        node.position+=targetWorld-currentWorld;
    }
    public void Highlight(string name,bool hover,bool pressed)
    {
        var node=nodes.FirstOrDefault(n=>n.name==name);
        if(node==null)return;
        foreach(var renderer in node.GetComponentsInChildren<Renderer>())
            foreach(var material in renderer.sharedMaterials)
            {
                Color tint;if(!colors.TryGetValue(material,out tint))continue;
                float brightness=pressed?.9f:hover?1.35f:1;
                material.SetColor("_Tint",new Color(tint.r*brightness,tint.g*brightness,tint.b*brightness,tint.a));
            }
    }
}
