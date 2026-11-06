using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace NetworkTest_Yajima.Editor
{
    public static class RoomLobbyBuilder
    {
        private static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        [MenuItem("Tools/Yajima/Create Room Lobby %#l")]
        public static void Build()
        {
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var root = new GameObject("RoomLobbyUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(RoomLobbyUI));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280,800); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var bg = Panel(root.transform,"Background",0,0,1280,800,new Color32(12,20,34,255));
            bg.anchorMin=Vector2.zero; bg.anchorMax=Vector2.one; bg.offsetMin=bg.offsetMax=Vector2.zero;
            var layout = Rect(root.transform,"Layout",0,0,1280,800);
            layout.anchorMin=layout.anchorMax=layout.pivot=new Vector2(.5f,.5f); layout.anchoredPosition=Vector2.zero;
            Label(layout,"Eyebrow","YAJIMA / MULTIPLAYER LOBBY",40,24,1100,24,15);
            Label(layout,"RoomTitle","ROOM LOBBY",40,62,1150,50,32);
            Label(layout,"Intro","Choose your character and get ready together.",40,118,1150,30,18);
            var members=Panel(layout,"PlayersPanel",40,174,560,490,new Color32(24,36,55,255));
            Label(members,"PlayersHeading","PLAYERS / SELECTION / READY",24,18,510,30,19);
            var scroll=Rect(members,"MemberScroll",24,65,512,400); var sr=scroll.gameObject.AddComponent<ScrollRect>();
            scroll.gameObject.AddComponent<RectMask2D>();
            var text=Label(scroll,"Members","Waiting for room state...",0,0,512,400,19);
            text.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            sr.content=text.rectTransform; sr.viewport=scroll; sr.horizontal=false; sr.movementType=ScrollRect.MovementType.Clamped;
            var choose=Panel(layout,"SelectionPanel",624,174,616,490,new Color32(24,36,55,255));
            Label(choose,"ChooseHeading","SELECT YOUR CHARACTER",24,18,568,30,19);
            for(int i=0;i<3;i++) Button(choose,"Character"+i,"CHARACTER "+(char)('A'+i),24+i*192,80,176);
            Label(choose,"Selection","YOUR SELECTION: CHARACTER A",24,166,568,35,21);
            Label(choose,"Hint","Temporary choices for the lobby UI.\nChanging character cancels your ready status.\nThese choices do not change the ingame prefab yet.",24,222,568,110,17);
            Button(choose,"Ready","READY",24,380,568);
            Button(layout,"LeaveRoom","LEAVE ROOM",40,692,250);
            Button(layout,"StartGame","START GAME / OWNER ONLY",836,692,404);
            Label(layout,"Status","Choose a character, then press READY.",314,690,498,70,17);
            var prefab=PrefabUtility.SaveAsPrefabAssetAndConnect(root,"Assets/Mock/Prefabs/Net/Yajima/RoomLobbyUI.prefab",InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene,RoomSceneTransition.LobbyPath);
            EditorSceneManager.CloseScene(scene,true);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
            var builds=EditorBuildSettings.scenes.Where(s=>s.path!=RoomSceneTransition.LobbyPath).ToList(); builds.Add(new EditorBuildSettingsScene(RoomSceneTransition.LobbyPath,true)); EditorBuildSettings.scenes=builds.ToArray();
            var rig=PrefabUtility.LoadPrefabContents("Assets/Mock/Prefabs/Net/Yajima/NetTestConnection.prefab");
            foreach(var t in rig.GetComponentsInChildren<Text>(true))
            {
                if(t.name=="Footer") t.text="Create or join a room to enter the lobby. Ready and character selection are available there.";
                if(t.name=="RoomTitle") t.text="NEXT: ROOM LOBBY";
                if(t.name=="Members") t.text="Create a room, or choose JOIN from the list.\nYou will move to the lobby automatically.";
            }
            foreach(var b in rig.GetComponentsInChildren<Button>(true)) if(b.name=="Ready"||b.name=="Start"||b.name=="Leave") b.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(rig,"Assets/Mock/Prefabs/Net/Yajima/NetTestConnection.prefab"); PrefabUtility.UnloadPrefabContents(rig);
            AssetDatabase.SaveAssets(); Debug.Log("[RoomLobbyBuilder] Lobby scene and prefab saved.");
        }
        private static RectTransform Rect(Transform p,string n,float x,float y,float w,float h)
        {var r=new GameObject(n,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(p,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
        private static RectTransform Panel(Transform p,string n,float x,float y,float w,float h,Color c)
        {var r=Rect(p,n,x,y,w,h);r.gameObject.AddComponent<Image>().color=c;return r;}
        private static Text Label(Transform p,string n,string v,float x,float y,float w,float h,int size)
        {var t=Rect(p,n,x,y,w,h).gameObject.AddComponent<Text>();t.font=Font;t.fontSize=size;t.color=new Color32(225,234,248,255);t.text=v;t.supportRichText=false;t.raycastTarget=false;return t;}
        private static void Button(Transform p,string n,string v,float x,float y,float w)
        {var r=Panel(p,n,x,y,w,54,new Color32(35,106,180,255));var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();var t=Label(r,"Label",v,8,0,w-16,54,16);t.alignment=TextAnchor.MiddleCenter;}
    }
}
